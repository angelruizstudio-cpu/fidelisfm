namespace CFS.Core.Services;

public interface IEmailService
{
    Task SendWelcomeAsync(string toEmail, string toName, string orgName, string setPasswordLink, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetLink, CancellationToken cancellationToken = default);

    /// <summary>
    /// Onboarding email sent to a newly self-provisioned church after a successful Stripe signup.
    /// The admin already chose their password during checkout, so this confirms the account is
    /// ready and points them to the login page (no credential is included).
    /// </summary>
    Task SendTenantWelcomeAsync(string toEmail, string orgName, string loginUrl, CancellationToken cancellationToken = default);
}
