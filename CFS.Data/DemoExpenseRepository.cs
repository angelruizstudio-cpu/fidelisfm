using CFS.Core.Models;
using CFS.Core.Services;

namespace CFS.Data;

public sealed class DemoExpenseRepository : IExpenseRepository
{
    public Task<ExpenseLookups> GetLookupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExpenseLookups(
            DemoData.Accounts,
            DemoData.ExpenseSubcategories,
            ["Cheque", "ACH", "Tarjeta", "Transferencia", "Efectivo", "Zelle"]));

    public Task<IReadOnlyList<ExpenseTransaction>> GetRecentAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var start = startDate?.Date ?? DateTime.Today.AddDays(-30);
        var end = (endDate?.Date ?? DateTime.Today).AddDays(1);
        var rows = DemoData.Expenses
            .Where(e => e.Date >= start && e.Date < end)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExpenseTransaction>>(rows);
    }

    public Task<ExpenseTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(DemoData.Expenses.FirstOrDefault(e => e.Id == id));

    public Task<ExpenseSaveResult> SaveAsync(
        ExpenseEntry entry,
        string userName,
        bool ignorePossibleDuplicate = false,
        CancellationToken cancellationToken = default)
    {
        var id = entry.Id > 0 ? entry.Id : DemoData.NextExpenseId++;
        var account = DemoData.Accounts.FirstOrDefault(a => a.Id == entry.AccountId) ?? DemoData.Accounts[0];
        var subcategory = DemoData.ExpenseSubcategories.FirstOrDefault(s => s.Id == entry.SubcategoryId) ?? DemoData.ExpenseSubcategories[0];

        var existing = DemoData.Expenses.FirstOrDefault(e => e.Id == id);
        if (existing is not null)
        {
            DemoData.AccountBalances[existing.AccountId] += existing.Amount;
        }

        var row = new ExpenseTransaction(
            id,
            entry.Date.Date,
            entry.Description,
            entry.Amount,
            account.Id,
            account.Name,
            subcategory.Id,
            subcategory.Name,
            entry.PaymentMethod,
            entry.CheckNumber,
            IsReconciled: false,
            IsVoided: false);

        DemoData.Expenses.RemoveAll(e => e.Id == id);
        DemoData.Expenses.Insert(0, row);
        DemoData.AccountBalances[account.Id] -= entry.Amount;
        return Task.FromResult(new ExpenseSaveResult(true, id, false, null));
    }

    public Task<ExpenseSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var existing = DemoData.Expenses.FirstOrDefault(e => e.Id == id);
        if (existing is null)
        {
            return Task.FromResult(new ExpenseSaveResult(false, null, false, "No se encontro el egreso demo."));
        }

        DemoData.Expenses.Remove(existing);
        DemoData.Expenses.Insert(0, existing with { IsVoided = true });
        DemoData.AccountBalances[existing.AccountId] += existing.Amount;
        return Task.FromResult(new ExpenseSaveResult(true, id, false, null));
    }
}
