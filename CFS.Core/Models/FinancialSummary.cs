namespace CFS.Core.Models;

public sealed record FinancialSummary(
    decimal YearToDateIncome,
    decimal YearToDateExpenses,
    decimal YearToDateBalance);

