namespace CFS.Core.Services;

public interface IEmailService
{
    Task SendWelcomeAsync(string toEmail, string toName, string orgName, string setPasswordLink, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetLink, CancellationToken cancellationToken = default);
}
