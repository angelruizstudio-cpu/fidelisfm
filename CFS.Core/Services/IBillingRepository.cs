namespace CFS.Core.Services;

public interface IBillingRepository
{
    /// <summary>
    /// Called from the Stripe webhook after an add-on checkout completes. Grants the
    /// add-on's feature keys to the tenant via TenantFeatureOverrides. Idempotent.
    /// </summary>
    Task GrantAddonFeaturesAsync(
        int tenantId,
        IReadOnlyList<string> featureKeys,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called from the Stripe webhook after a plan upgrade checkout completes. Switches the
    /// tenant's active plan and records the new Stripe subscription, returning the previous
    /// Stripe subscription id (if any) so the caller can cancel it to avoid double billing.
    /// </summary>
    Task<string?> CompleteUpgradeAsync(
        int tenantId,
        string newPlanKey,
        string? newStripeSubscriptionId,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default);
}
