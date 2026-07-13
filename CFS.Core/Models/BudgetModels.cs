namespace CFS.Core.Models;

public static class BudgetCategoryType
{
    public const string Income = "Ingreso";
    public const string Expense = "Egreso";
}

/// <summary>
/// One category's budget vs. actual for the selected year, accumulated
/// through the selected month.
/// </summary>
public sealed record BudgetCategoryLine(
    int CategoryId,
    string CategoryName,
    string Type,
    decimal AnnualBudget,
    decimal ExpectedToDate,
    decimal ActualToDate)
{
    public bool IsIncome => Type == BudgetCategoryType.Income;

    /// <summary>Planned amount for one month (annual / 12).</summary>
    public decimal MonthlyBudget => Math.Round(AnnualBudget / 12m, 2);

    /// <summary>
    /// Favorable variance (positive = good) for both directions:
    /// expenses under plan and income over plan are both favorable.
    /// </summary>
    public decimal Variance => IsIncome
        ? ActualToDate - ExpectedToDate
        : ExpectedToDate - ActualToDate;

    /// <summary>Percent of the ANNUAL budget consumed so far (bar fill).</summary>
    public int PercentOfAnnual => AnnualBudget <= 0
        ? 0
        : (int)Math.Round(ActualToDate / AnnualBudget * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Percent of the EXPECTED-to-date amount (on-track gauge).</summary>
    public int PercentOfExpected => ExpectedToDate <= 0
        ? 0
        : (int)Math.Round(ActualToDate / ExpectedToDate * 100m, MidpointRounding.AwayFromZero);
}

public sealed record BudgetOverview(
    int Year,
    int Month,
    IReadOnlyList<BudgetCategoryLine> IncomeLines,
    IReadOnlyList<BudgetCategoryLine> ExpenseLines)
{
    public decimal IncomeAnnualBudget => IncomeLines.Sum(l => l.AnnualBudget);
    public decimal IncomeActual => IncomeLines.Sum(l => l.ActualToDate);
    public decimal IncomeExpected => IncomeLines.Sum(l => l.ExpectedToDate);

    public decimal ExpenseAnnualBudget => ExpenseLines.Sum(l => l.AnnualBudget);
    public decimal ExpenseActual => ExpenseLines.Sum(l => l.ActualToDate);
    public decimal ExpenseExpected => ExpenseLines.Sum(l => l.ExpectedToDate);
}
