namespace CFS.Core.Models;

public sealed record ReportDefinition(
    string Key,
    string Name,
    string Description,
    bool IsReady);

public sealed record ReportRequest(
    string Key,
    DateTime StartDate,
    DateTime EndDate,
    int? AccountId = null);

public sealed record ReportBankAccount(
    int Id,
    string Name);

public sealed record FinancialReport(
    string Key,
    string Title,
    string PeriodLabel,
    IReadOnlyList<ReportSection> Sections,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetIncome,
    IReadOnlyList<ReportInsight> Insights,
    DateTime GeneratedAt);

public sealed record ReportSection(
    string Name,
    IReadOnlyList<ReportLine> Lines,
    decimal Total);

public sealed record ReportLine(
    string Key,
    string Label,
    decimal Amount,
    int Level,
    bool IsTotal,
    bool HasInsight,
    IReadOnlyList<ReportDetailLine> Details);

public sealed record ReportDetailLine(
    DateTime Date,
    string Description,
    string Name,
    string AccountName,
    string CategoryName,
    decimal Amount,
    string Reference);

public sealed record ReportInsight(
    string LineKey,
    string Title,
    string Summary,
    string Severity,
    decimal? ImpactAmount = null,
    decimal? ImpactPercent = null,
    string? RootCause = null);
