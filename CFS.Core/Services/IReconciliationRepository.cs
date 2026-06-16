using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IReconciliationRepository
{
    Task<ReconciliationLookups> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<ReconciliationWorkspace> GetWorkspaceAsync(
        int accountId,
        DateTime statementDate,
        CancellationToken cancellationToken = default);

    Task<ReconciliationSaveResult> CloseAsync(
        ReconciliationEntry entry,
        string userName,
        CancellationToken cancellationToken = default);
}
