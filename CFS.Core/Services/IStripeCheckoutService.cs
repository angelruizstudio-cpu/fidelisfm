namespace CFS.Core.Services;

public sealed record CheckoutSessionRequest(
    string PlanKey,
    string BillingCycle,
    string OrganizationName,
    string Email,
    string Phone,
    string SuccessUrl,
    string CancelUrl);

public interface IStripeCheckoutService
{
    /// <summary>
    /// Creates a Stripe-hosted Checkout Session for the given plan/billing cycle and returns
    /// the URL the browser should be redirected to in order to complete payment.
    /// </summary>
    Task<string> CreateCheckoutSessionUrlAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default);
}
