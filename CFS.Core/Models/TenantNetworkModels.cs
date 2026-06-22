namespace CFS.Core.Models;

/// <summary>
/// Pricing for the Multi-Iglesia plan: a fixed base price covers up to
/// BaseChurchesIncluded churches; each additional block of ChurchesPerIncrement
/// churches costs PricePerIncrementMonthly more per month.
/// </summary>
public static class CfsTenantNetworkPricing
{
    public const decimal BaseMonthlyPrice = 250m;
    public const int BaseChurchesIncluded = 10;
    public const int ChurchesPerIncrement = 5;
    public const decimal PricePerIncrementMonthly = 125m;

    public static decimal GetMonthlyPriceForChurchLimit(int maxChurches)
    {
        if (maxChurches <= BaseChurchesIncluded)
        {
            return BaseMonthlyPrice;
        }

        var extraChurches = maxChurches - BaseChurchesIncluded;
        var increments = (int)Math.Ceiling(extraChurches / (double)ChurchesPerIncrement);
        return BaseMonthlyPrice + (increments * PricePerIncrementMonthly);
    }
}

public sealed record TenantNetwork(
    int Id,
    string Name,
    string Slug,
    int MaxChurches,
    DateTime CreatedAt);

public sealed record TenantNetworkSubscription(
    int Id,
    int TenantNetworkId,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    string Status,
    DateTime StartedAt,
    DateTime? CurrentPeriodEndsAt);

/// <summary>
/// Grants a user access to a church (tenant) beyond their primary/home tenant,
/// with its own set of roles for that specific church.
/// </summary>
public sealed record UserTenantAccess(
    int Id,
    int UserId,
    int TenantId,
    IReadOnlyList<string> RoleKeys);
