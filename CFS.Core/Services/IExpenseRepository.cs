using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IExpenseRepository
{
    Task<ExpenseLookups> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseTransaction>> GetRecentAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<ExpenseTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ExpenseSaveResult> SaveAsync(
        ExpenseEntry entry,
        string userName,
        bool ignorePossibleDuplicate = false,
        CancellationToken cancellationToken = default);

    Task<ExpenseSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default);
}
