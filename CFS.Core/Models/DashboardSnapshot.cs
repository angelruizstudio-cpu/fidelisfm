namespace CFS.Core.Models;

public sealed record DashboardSnapshot(
    FinancialSummary Summary,
    IReadOnlyList<BankAccountBalance> BankAccounts,
    IReadOnlyList<KpiTrend> Kpis,
    DateTime LoadedAt);

public sealed record MonthlyTrendPoint(string MonthLabel, decimal Income, decimal Expenses);

