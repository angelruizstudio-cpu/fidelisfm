namespace CFS.Core.Services;

public sealed record PendingSignup(
    string OrganizationName,
    string Email,
    string Phone,
    string PlanKey,
    string BillingCycle,
    string StripeSessionId,
    string? StripeCustomerId);

public interface ISignupRepository
{
    /// <summary>
    /// Records a completed Stripe Checkout payment so it can be reviewed and provisioned into
    /// a tenant/user account. Idempotent on StripeSessionId — replaying the same webhook event
    /// must not create duplicate rows.
    /// </summary>
    Task RecordPendingSignupAsync(PendingSignup signup, CancellationToken cancellationToken = default);
}
