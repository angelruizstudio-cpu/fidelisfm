using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IBudgetRepository
{
    /// <summary>
    /// Returns every income/expense category with its annual budget for the year
    /// and the actual amount accumulated from Jan 1 through the end of the given month.
    /// </summary>
    Task<BudgetOverview> GetOverviewAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets (inserts or updates) the annual budget for a single category and year.
    /// </summary>
    Task SaveCategoryBudgetAsync(int categoryId, int year, decimal annualAmount, string userName, CancellationToken cancellationToken = default);
}
