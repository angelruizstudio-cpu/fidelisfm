using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IReportRepository
{
    Task<IReadOnlyList<ReportDefinition>> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportBankAccount>> GetBankAccountsAsync(CancellationToken cancellationToken = default);

    Task<FinancialReport> GetReportAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default);
}
