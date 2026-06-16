namespace CFS.Core.Models;

public sealed record DashboardSnapshot(
    FinancialSummary Summary,
    IReadOnlyList<BankAccountBalance> BankAccounts,
    DateTime LoadedAt);

