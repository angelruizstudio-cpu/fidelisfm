using System.Security.Claims;
using CFS.Core.Models;

namespace CFS.Core.Services;

public interface ISubscriptionService
{
    Task<TenantSubscription> GetCurrentAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<bool> HasFeatureAsync(
        ClaimsPrincipal user,
        string featureKey,
        CancellationToken cancellationToken = default);
}
