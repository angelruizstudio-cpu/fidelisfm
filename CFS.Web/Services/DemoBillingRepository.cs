using CFS.Core.Services;

namespace CFS.Web.Services;

public sealed class DemoBillingRepository : IBillingRepository
{
    public Task GrantAddonFeaturesAsync(
        int tenantId,
        IReadOnlyList<string> featureKeys,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> CompleteUpgradeAsync(
        int tenantId,
        string newPlanKey,
        string? newStripeSubscriptionId,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
