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

public interface IStripeCheckoutService
{
    /// <summary>
    /// Creates a Stripe-hosted Checkout Session for the given plan/billing cycle.
    /// </summary>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default);
}
