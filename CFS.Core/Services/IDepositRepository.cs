using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IDepositRepository
{
    Task<DepositLookups> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepositCandidate>> GetPendingCandidatesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepositSummary>> GetRecentAsync(CancellationToken cancellationToken = default);

    Task<DepositSaveResult> CreateAsync(
        DepositEntry entry,
        string userName,
        CancellationToken cancellationToken = default);

    Task<DepositBatchSaveResult> CreateBatchAsync(
        IReadOnlyList<DepositEntry> entries,
        string userName,
        CancellationToken cancellationToken = default);

    Task<DepositSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default);
}
