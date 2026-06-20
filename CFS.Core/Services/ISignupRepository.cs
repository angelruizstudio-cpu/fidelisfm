namespace CFS.Core.Services;

public sealed record PendingSignup(
    string OrganizationName,
    string Email,
    string Phone,
    string PlanKey,
    string BillingCycle,
    string StripeSessionId,
    string? StripeCustomerId);

public sealed record PendingSignupRecord(
    int Id,
    string OrganizationName,
    string Email,
    string PlanKey,
    string BillingCycle,
    string Status,
    string StripeSessionId,
    int? ProvisionedTenantId,
    DateTime CreatedAt,
    DateTime? ProvisionedAt);

public interface ISignupRepository
{
    /// <summary>
    /// Persists a checkout attempt (with hashed password) right before the customer is redirected
    /// to Stripe, keyed by the Stripe Checkout Session id so the webhook can find it later.
    /// </summary>
    Task CreatePendingSignupAsync(PendingSignup signup, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called from the Stripe webhook once payment succeeds. Provisions a new Tenant,
    /// TenantSubscription, and Usuario (Administrador role) from the pending signup row, then
    /// marks it Provisioned. Idempotent — replaying the same event returns the same TenantId
    /// without creating duplicates. Returns null if no matching pending signup is found.
    /// </summary>
    Task<int?> CompleteSignupAndProvisionTenantAsync(
        string stripeSessionId,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the most recent pending signups (any status) for the admin view, newest first.
    /// </summary>
    Task<IReadOnlyList<PendingSignupRecord>> ListRecentAsync(int take, CancellationToken cancellationToken = default);
}
