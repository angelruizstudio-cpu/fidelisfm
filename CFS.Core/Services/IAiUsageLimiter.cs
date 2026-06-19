namespace CFS.Core.Services;

public sealed record AiUsageStatus(int Used, int Limit, bool IsAllowed);

public interface IAiUsageLimiter
{
    /// <summary>
    /// Checks whether the current tenant (from <see cref="ITenantContext"/>) is within its
    /// monthly AI question quota for the given plan. If allowed, atomically increments the
    /// month's usage counter. If the tenant is already at or over the limit, the counter is
    /// left untouched and <see cref="AiUsageStatus.IsAllowed"/> is false.
    /// </summary>
    Task<AiUsageStatus> CheckAndIncrementAsync(string planKey, CancellationToken cancellationToken = default);
}
