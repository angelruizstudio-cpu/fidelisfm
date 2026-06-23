using CFS.Core.Models;
using CFS.Core.Services;

namespace CFS.Web.Services;

public sealed class DemoSalesLeadRepository : ISalesLeadRepository
{
    public Task CreateAsync(SalesLead lead, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
