using CFS.Core.Models;
using CFS.Core.Services;

namespace CFS.Web.Services;

/// <summary>
/// No-op organization label service for Demo mode: always returns Church defaults,
/// since the demo environment has no database-backed per-tenant organization settings.
/// </summary>
public sealed class DemoOrganizationLabelService : IOrganizationLabelService
{
    public Task<OrganizationLabels> GetLabelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(OrganizationLabelDefaults.GetDefaults(OrganizationType.Church));

    public Task<OrganizationSettings?> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<OrganizationSettings?>(null);
}
