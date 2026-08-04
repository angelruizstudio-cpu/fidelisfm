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

    /// <summary>
    /// Reverses a finalized reconciliation: un-clears the deposits and transactions it cleared and
    /// deletes the reconciliation record, so those items become available to reconcile again.
    /// Items are matched by recorded membership (ID_Conciliacion_FK); reconciliations closed before
    /// membership tracking existed fall back to un-clearing items in their statement-date window.
    /// </summary>
    Task<ReconciliationSaveResult> VoidAsync(
        int reconciliationId,
        string userName,
        CancellationToken cancellationToken = default);
}
