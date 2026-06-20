namespace CFS.Core.Services;

public sealed record CheckoutSessionRequest(
    string PlanKey,
    string BillingCycle,
    string OrganizationName,
    string Email,
    string Phone,
    string SuccessUrl,
    string CancelUrl);

public sealed record CheckoutSessionResult(string Url, string SessionId);

public sealed record AddonCheckoutRequest(
    int TenantId,
    string Email,
    string AddonKey,
    string SuccessUrl,
    string CancelUrl);

public sealed record UpgradeCheckoutRequest(
    int TenantId,
    string Email,
    string NewPlanKey,
    string SuccessUrl,
    string CancelUrl);

public interface IStripeCheckoutService
{
    /// <summary>
    /// Creates a Stripe-hosted Checkout Session for the given plan/billing cycle.
    /// </summary>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe-hosted Checkout Session for a paid add-on subscription.
    /// </summary>
    Task<CheckoutSessionResult> CreateAddonCheckoutSessionAsync(AddonCheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe-hosted Checkout Session for upgrading to a higher plan, applying a
    /// 25%-off-first-month coupon. The webhook is responsible for cancelling the tenant's
    /// previous subscription once this new one is confirmed.
    /// </summary>
    Task<CheckoutSessionResult> CreateUpgradeCheckoutSessionAsync(UpgradeCheckoutRequest request, CancellationToken cancellationToken = default);
}
