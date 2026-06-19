using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IOrganizationLabelService
{
    Task<OrganizationLabels> GetLabelsAsync(CancellationToken cancellationToken = default);
    Task<OrganizationSettings?> GetSettingsAsync(CancellationToken cancellationToken = default);
}
