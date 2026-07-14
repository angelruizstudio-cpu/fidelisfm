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

/// <summary>
/// Outcome of a provisioning call. Carries the info the caller needs to send a welcome email.
/// <paramref name="AlreadyProvisioned"/> is true when the signup had already been provisioned
/// (idempotent replay) — in that case the welcome email should NOT be sent again.
/// </summary>
public sealed record SignupProvisionResult(
    int TenantId,
    string Email,
    string OrganizationName,
    bool AlreadyProvisioned);

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
    /// marks it Provisioned. Idempotent — replaying the same event returns the same tenant with
    /// AlreadyProvisioned=true. Returns null if no matching pending signup is found (or it has no
    /// stored password). If provisioning throws, the pending signup is marked 'Failed'.
    /// </summary>
    Task<SignupProvisionResult?> CompleteSignupAndProvisionTenantAsync(
        string stripeSessionId,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the most recent pending signups (any status) for the admin view, newest first.
    /// </summary>
    Task<IReadOnlyList<PendingSignupRecord>> ListRecentAsync(int take, CancellationToken cancellationToken = default);
}
