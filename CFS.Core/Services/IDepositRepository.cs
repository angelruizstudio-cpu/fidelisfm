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

    /// <summary>
    /// Updates the date of an existing deposit. Only allowed while the deposit is still editable
    /// (not voided and not part of a finalized reconciliation), so a date change can never corrupt
    /// a closed reconciliation.
    /// </summary>
    Task<DepositSaveResult> UpdateDateAsync(
        int id,
        DateTime newDate,
        string userName,
        CancellationToken cancellationToken = default);
}
