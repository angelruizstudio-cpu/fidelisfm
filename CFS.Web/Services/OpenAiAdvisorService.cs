using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.AspNetCore.Http;

namespace CFS.Web.Services;

public sealed class OpenAiAdvisorService(
    HttpClient httpClient,
    IConfiguration configuration,
    IDashboardRepository dashboardRepository,
    IReportRepository reportRepository,
    IAiUsageLimiter aiUsageLimiter,
    IHttpContextAccessor httpContextAccessor) : IAiAdvisorService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiAnswer> AskAsync(
        AiQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var planKey = httpContextAccessor.HttpContext?.User?.FindFirst("PlanKey")?.Value ?? CfsPlans.Basic;
        var usage = await aiUsageLimiter.CheckAndIncrementAsync(planKey, cancellationToken);
        if (!usage.IsAllowed)
        {
            var isFounderOrMultiChurch = planKey.Equals(CfsPlans.Founder, StringComparison.OrdinalIgnoreCase) ||
                planKey.Equals(CfsPlans.MultiChurch, StringComparison.OrdinalIgnoreCase);

            var upgradeHint = isFounderOrMultiChurch
                ? string.Empty
                : " Considera actualizar tu plan para aumentar este límite.";

            return new AiAnswer(
                $"Has alcanzado el límite de {usage.Limit} preguntas al asistente IA para este mes en tu plan actual. " +
                $"El contador se reinicia el primer día del próximo mes.{upgradeHint}",
                [new AiCitation("Plan de suscripción", "Límite mensual de IA", $"{usage.Used}/{usage.Limit}")],
                ["¿Cuál es el balance en libros por cuenta?", "Resume el Profit and Loss de este año"]);
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiAnswer(
                "El agente IA ya está conectado en código, pero falta configurar la llave de OpenAI. Configura `OpenAI:ApiKey` en User Secrets o la variable de ambiente `OPENAI_API_KEY` y vuelve a preguntar.",
                [new AiCitation("Configuración", "OpenAI:ApiKey / OPENAI_API_KEY", null)],
                ["¿Cuál es el balance en libros por cuenta?", "Resume el Profit and Loss de este año"]);
        }

        var start = request.StartDate ?? new DateTime(DateTime.Today.Year, 1, 1);
        var end = request.EndDate ?? DateTime.Today;
        var context = await BuildFinancialContextAsync(start, end, cancellationToken);

        var payload = new
        {
            model = configuration["OpenAI:Model"] ?? "gpt-4.1-mini",
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = """
                    Eres el agente financiero de Fidelis Financial Management.
                    Responde en español claro y profesional.
                    Usa solo el contexto financiero provisto por Fidelis para cantidades, saldos, reportes y conclusiones.
                    Si falta información, dilo claramente y sugiere qué verificar en Fidelis.
                    No inventes transacciones, clientes, bancos ni saldos.
                    Distingue siempre entre saldo en libros Fidelis y saldo real del banco.
                    """
                },
                new
                {
                    role = "user",
                    content = $"""
                    Pregunta del usuario:
                    {request.Question}

                    Periodo solicitado:
                    {start:yyyy-MM-dd} a {end:yyyy-MM-dd}

                    Contexto interno de Fidelis:
                    {context}
                    """
                }
            },
            max_output_tokens = 900
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, GetResponsesUrl());
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new AiAnswer(
                $"OpenAI respondió con error {(int)response.StatusCode}. Verifica la llave, el modelo configurado y que el servidor tenga salida a internet. Detalle: {SummarizeError(responseBody)}",
                [new AiCitation("OpenAI", "Responses API", response.StatusCode.ToString())],
                ["¿Cómo verifico la configuración de OpenAI?", "¿Qué modelo está configurado?"]);
        }

        var answer = ExtractOutputText(responseBody);
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = "OpenAI respondió, pero Fidelis no pudo leer texto de salida. Hay que revisar el formato de respuesta recibido.";
        }

        return new AiAnswer(
            answer,
            [
                new AiCitation("Dashboard", "Cuentas bancarias", null),
                new AiCitation("Reportes", "Profit and Loss", $"Desde {start:MM/dd/yyyy} hasta {end:MM/dd/yyyy}"),
                new AiCitation("OpenAI", "Responses API", configuration["OpenAI:Model"] ?? "gpt-4.1-mini")
            ],
            ["¿Qué gastos debo revisar primero?", "¿Qué ingresos explican mejor el periodo?", "¿Cómo comparo esto con conciliación bancaria?"]);
    }

    private async Task<string> BuildFinancialContextAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var snapshot = await dashboardRepository.GetSnapshotAsync(cancellationToken);
        var report = await reportRepository.GetReportAsync(new ReportRequest("profit-loss", start, end), cancellationToken);

        var accounts = string.Join(Environment.NewLine, snapshot.BankAccounts.Select(account =>
            $"- {account.Name}: saldo en libros Fidelis {account.CurrentBalance:C2}"));

        var incomeLines = FlattenLines(report, "Income")
            .Where(line => !line.IsTotal)
            .OrderByDescending(line => line.Amount)
            .Take(8)
            .Select(line => $"- {line.Label}: {line.Amount:C2}");

        var expenseLines = FlattenLines(report, "Expenses")
            .Where(line => !line.IsTotal && line.Level > 1)
            .OrderByDescending(line => line.Amount)
            .Take(8)
            .Select(line => $"- {line.Label}: {line.Amount:C2}");

        var insights = report.Insights
            .Take(6)
            .Select(insight => $"- {insight.Title}: {insight.Summary}");

        return $"""
        Saldos en libros Fidelis:
        {accounts}

        Resumen Profit and Loss:
        - Periodo: {report.PeriodLabel}
        - Ingresos: {report.TotalIncome:C2}
        - Gastos: {report.TotalExpenses:C2}
        - Net income: {report.NetIncome:C2}

        Ingresos principales:
        {string.Join(Environment.NewLine, incomeLines)}

        Gastos principales:
        {string.Join(Environment.NewLine, expenseLines)}

        Insights calculados por Fidelis:
        {string.Join(Environment.NewLine, insights)}
        """;
    }

    private static IEnumerable<ReportLine> FlattenLines(FinancialReport report, string sectionName) =>
        report.Sections
            .Where(section => section.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(section => section.Lines);

    private string? GetApiKey() =>
        configuration["OpenAI:ApiKey"] ??
        Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private string GetResponsesUrl()
    {
        var baseUrl = configuration["OpenAI:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://api.openai.com/v1";
        }

        return $"{baseUrl.TrimEnd('/')}/responses";
    }

    private static string ExtractOutputText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (!document.RootElement.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    parts.Add(text.GetString() ?? string.Empty);
                }
            }
        }

        return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string SummarizeError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return "sin cuerpo de respuesta.";
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? responseBody;
            }
        }
        catch (JsonException)
        {
            // Return a clipped plain response below.
        }

        return responseBody.Length > 300 ? responseBody[..300] : responseBody;
    }
}
