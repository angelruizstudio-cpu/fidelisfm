using CFS.Core.Models;
using CFS.Core.Services;

namespace CFS.Web.Services;

/// <summary>
/// No-op AI usage limiter for Demo mode: always allows the request without persisting
/// any usage, since the demo environment has no database-backed tenant usage tracking.
/// </summary>
public sealed class DemoAiUsageLimiter : IAiUsageLimiter
{
    public Task<AiUsageStatus> CheckAndIncrementAsync(string planKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AiUsageStatus(0, CfsAiQuotas.GetMonthlyLimit(planKey), IsAllowed: true));
}
