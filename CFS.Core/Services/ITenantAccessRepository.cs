using CFS.Core.Models;

namespace CFS.Core.Services;

/// <summary>
/// Resolves which churches a logged-in user is allowed to switch their active
/// session into: their home church plus any granted via UserTenantAccess.
/// </summary>
public interface ITenantAccessRepository
{
    Task<IReadOnlyList<TenantAccessOption>> GetAccessibleTenantsAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
