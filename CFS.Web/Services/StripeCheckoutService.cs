using CFS.Core.Models;
using CFS.Core.Services;
using Stripe;
using Stripe.Checkout;

namespace CFS.Web.Services;

public sealed class StripeCheckoutService(IConfiguration configuration) : IStripeCheckoutService
{
    public async Task<string> CreateCheckoutSessionUrlAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        var secretKey = configuration["STRIPE_SECRET_KEY"]
            ?? throw new InvalidOperationException("STRIPE_SECRET_KEY is not configured.");
        var priceId = ResolvePriceId(request.PlanKey, request.BillingCycle);

        var client = new StripeClient(secretKey);
        var service = new SessionService(client);

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            CustomerEmail = request.Email,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["OrganizationName"] = request.OrganizationName,
                ["Phone"] = request.Phone,
                ["PlanKey"] = request.PlanKey,
                ["BillingCycle"] = request.BillingCycle,
            },
        };

        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        return session.Url;
    }

    private string ResolvePriceId(string planKey, string billingCycle)
    {
        var configKey = (planKey.ToLowerInvariant(), billingCycle.ToLowerInvariant()) switch
        {
            (CfsPlans.Basic, "monthly") => "STRIPE_PRICE_BASIC_MONTHLY",
            (CfsPlans.Basic, "annual") => "STRIPE_PRICE_BASIC_YEARLY",
            (CfsPlans.Standard, "monthly") => "STRIPE_PRICE_STANDARD_MONTHLY",
            (CfsPlans.Standard, "annual") => "STRIPE_PRICE_STANDARD_YEARLY",
            (CfsPlans.Pro, "monthly") => "STRIPE_PRICE_PRO_MONTHLY",
            (CfsPlans.Pro, "annual") => "STRIPE_PRICE_PRO_YEARLY",
            _ => throw new InvalidOperationException($"No Stripe price is configured for plan '{planKey}' / '{billingCycle}'."),
        };

        return configuration[configKey]
            ?? throw new InvalidOperationException($"Configuration key '{configKey}' is not set.");
    }
}
