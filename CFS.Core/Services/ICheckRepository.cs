using CFS.Core.Models;

namespace CFS.Core.Services;

public interface ICheckRepository
{
    Task<CheckLookups> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckVoucher>> GetRecentAsync(CancellationToken cancellationToken = default);

    Task<CheckVoucher?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<CheckSaveResult> SaveDraftAsync(
        CheckEntry entry,
        string userName,
        CancellationToken cancellationToken = default);

    Task<CheckSaveResult> MarkPrintedAsync(
        int id,
        string userName,
        CancellationToken cancellationToken = default);

    Task<CheckSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default);
}
