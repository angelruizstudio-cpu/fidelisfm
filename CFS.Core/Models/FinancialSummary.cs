namespace CFS.Core.Models;

public sealed record FinancialSummary(
    decimal YearToDateIncome,
    decimal YearToDateExpenses,
    decimal YearToDateBalance);

public sealed record KpiTrend(string Label, decimal CurrentMonth, decimal PreviousMonth)
{
    public decimal? ChangePercent => PreviousMonth == 0
        ? null
        : Math.Round((CurrentMonth - PreviousMonth) / Math.Abs(PreviousMonth) * 100, 1);

    public bool Increased => CurrentMonth >= PreviousMonth;
}

