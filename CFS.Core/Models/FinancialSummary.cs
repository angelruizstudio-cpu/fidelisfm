namespace CFS.Core.Models;

public sealed record FinancialSummary(
    decimal YearToDateIncome,
    decimal YearToDateExpenses,
    decimal YearToDateBalance);

public sealed record KpiTrend(string Label, decimal CurrentMonth, decimal PreviousMonth, bool LowerIsBetter = false)
{
    public decimal? ChangePercent => PreviousMonth == 0
        ? null
        : Math.Round((CurrentMonth - PreviousMonth) / Math.Abs(PreviousMonth) * 100, 1);

    public bool Increased => CurrentMonth >= PreviousMonth;

    /// <summary>True when there is a previous-month baseline to compare against.</summary>
    public bool HasComparison => PreviousMonth != 0;

    /// <summary>
    /// Whether the movement is good news: income going up is favorable,
    /// but for LowerIsBetter KPIs (expenses) going down is the favorable direction.
    /// </summary>
    public bool IsFavorable => LowerIsBetter ? !Increased : Increased;
}

