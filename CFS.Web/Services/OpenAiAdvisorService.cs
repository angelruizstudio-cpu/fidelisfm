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
                "El agente IA no tiene una llave configurada. Configura la variable de ambiente `GROQ_API_KEY` en Azure App Service y reinicia la aplicación.",
                [new AiCitation("Configuración", "GROQ_API_KEY", null)],
                ["¿Cuál es el balance en libros por cuenta?", "Resume el Profit and Loss de este año"]);
        }

        var start = request.StartDate ?? new DateTime(DateTime.Today.Year, 1, 1);
        var end = request.EndDate ?? DateTime.Today;
        var context = await BuildFinancialContextAsync(start, end, cancellationToken);
        var model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

        var payload = new
        {
            model,
            messages = new object[]
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
            max_tokens = 900,
            temperature = 0.3
        };

        var url = GetChatUrl();
        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new AiAnswer(
                $"Groq respondió con error {(int)response.StatusCode}. Verifica la llave y que el servidor tenga salida a internet. Detalle: {SummarizeError(responseBody)}",
                [new AiCitation("Groq", "Chat Completions API", response.StatusCode.ToString())],
                ["¿Cómo verifico la configuración del AI?", "¿Qué modelo está configurado?"]);
        }

        var answer = ExtractChatAnswer(responseBody);
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = "Groq respondió, pero no se pudo leer el texto de salida. Revisa el formato de respuesta.";
        }

        return new AiAnswer(
            answer,
            [
                new AiCitation("Dashboard", "Cuentas bancarias", null),
                new AiCitation("Reportes", "Profit and Loss", $"Desde {start:MM/dd/yyyy} hasta {end:MM/dd/yyyy}"),
                new AiCitation("Groq", model, "Chat Completions API")
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
        Environment.GetEnvironmentVariable("GROQ_API_KEY") ??
        configuration["Groq:ApiKey"];

    private string GetChatUrl()
    {
        var baseUrl = configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";
        return $"{baseUrl.TrimEnd('/')}/chat/completions";
    }

    private static string ExtractChatAnswer(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException) { }

        return string.Empty;
    }

    private static string SummarizeError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return "sin cuerpo de respuesta.";

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? responseBody;
            }
        }
        catch (JsonException) { }

        return responseBody.Length > 300 ? responseBody[..300] : responseBody;
    }
}
