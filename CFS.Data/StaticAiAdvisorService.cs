using CFS.Core.Models;
using CFS.Core.Services;

namespace CFS.Data;

public sealed class StaticAiAdvisorService(
    IDashboardRepository dashboardRepository,
    IReportRepository reportRepository) : IAiAdvisorService
{
    public async Task<AiAnswer> AskAsync(
        AiQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var question = request.Question.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            return new AiAnswer(
                "Escribe una pregunta sobre Fidelis o sobre las finanzas disponibles en el sistema.",
                [],
                ["¿Cuál es el balance en libros por cuenta?", "¿Cómo van los ingresos este año?"]);
        }

        var normalized = question.ToLowerInvariant();
        var start = request.StartDate ?? new DateTime(DateTime.Today.Year, 1, 1);
        var end = request.EndDate ?? DateTime.Today;

        if (IsSystemQuestion(normalized))
        {
            return BuildSystemAnswer();
        }

        if (IsBalanceQuestion(normalized))
        {
            var snapshot = await dashboardRepository.GetSnapshotAsync(cancellationToken);
            var accounts = string.Join(Environment.NewLine, snapshot.BankAccounts.Select(account =>
                $"- {account.Name}: {account.CurrentBalance:C2}"));

            return new AiAnswer(
                $"Estos son los saldos en libros Fidelis al momento de la consulta:{Environment.NewLine}{accounts}{Environment.NewLine}{Environment.NewLine}Recuerda: esto no es el balance en línea del banco; es el saldo calculado por Fidelis según saldo inicial y movimientos posteados.",
                [new AiCitation("Dashboard", "Cuentas bancarias", null)],
                ["¿Qué movimientos explican una cuenta?", "¿Cómo comparan esos saldos con la última conciliación?"]);
        }

        if (IsReportQuestion(normalized))
        {
            var report = await reportRepository.GetReportAsync(new ReportRequest("profit-loss", start, end), cancellationToken);
            var summary = $"Para el periodo {report.PeriodLabel}: ingresos {report.TotalIncome:C2}, gastos {report.TotalExpenses:C2}, net income {report.NetIncome:C2}.";
            var insight = report.Insights.FirstOrDefault();
            var insightText = insight is null ? string.Empty : $"{Environment.NewLine}{Environment.NewLine}Insight destacado: {insight.Summary}";

            return new AiAnswer(
                summary + insightText,
                [
                    new AiCitation("Reportes", "Profit and Loss", $"Desde {start:MM/dd/yyyy} hasta {end:MM/dd/yyyy}")
                ],
                ["¿Cuáles son los gastos principales?", "¿Qué ingresos tienen más peso?", "¿Por qué el net income cambió?"]);
        }

        return new AiAnswer(
            "Puedo ayudarte con preguntas de uso de Fidelis y con resúmenes financieros básicos usando Dashboard y Profit and Loss. Todavía falta conectar el proveedor real de IA para preguntas abiertas más profundas y análisis multi-tabla.",
            [
                new AiCitation("Fidelis", "Asistente IA inicial", null)
            ],
            ["¿Cuál es el balance en libros por cuenta?", "Resume el Profit and Loss de este año", "¿Qué puede hacer el módulo de depósitos?"]);
    }

    private static AiAnswer BuildSystemAnswer() =>
        new(
            "Fidelis tiene módulos para Dashboard, Ingresos, Egresos/Cheques, Depósitos, Conciliación bancaria y Reportes. El agente IA inicial puede orientar sobre el uso del sistema y resumir datos principales; la conexión a IA externa queda como próximo paso para análisis conversacional avanzado.",
            [new AiCitation("Fidelis", "Módulos del sistema", null)],
            ["¿Cómo funciona el módulo de depósitos?", "¿Qué significa saldo en libros Fidelis?", "¿Cómo se reconcilia una cuenta?"]);

    private static bool IsSystemQuestion(string question) =>
        question.Contains("como uso") ||
        question.Contains("cómo uso") ||
        question.Contains("modulo") ||
        question.Contains("módulo") ||
        question.Contains("funciona") ||
        question.Contains("cfs");

    private static bool IsBalanceQuestion(string question) =>
        question.Contains("balance") ||
        question.Contains("saldo") ||
        question.Contains("cuenta bancaria") ||
        question.Contains("cuentas bancarias");

    private static bool IsReportQuestion(string question) =>
        question.Contains("profit") ||
        question.Contains("loss") ||
        question.Contains("ingreso") ||
        question.Contains("gasto") ||
        question.Contains("net income") ||
        question.Contains("reporte");
}
