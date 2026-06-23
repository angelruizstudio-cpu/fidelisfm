using CFS.Core.Models;

namespace CFS.Core.Services;

public interface ISalesLeadRepository
{
    Task CreateAsync(SalesLead lead, CancellationToken cancellationToken = default);
}
