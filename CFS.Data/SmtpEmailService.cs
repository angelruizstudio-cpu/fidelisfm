using System.Net;
using System.Net.Mail;
using CFS.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CFS.Data;

public sealed class SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendWelcomeAsync(string toEmail, string toName, string orgName, string setPasswordLink, CancellationToken cancellationToken = default)
    {
        var subject = $"Bienvenido a Fidelis Financial Management — {orgName}";
        var body = $"""
            <p>Hola {toName},</p>
            <p>Tu cuenta en <strong>Fidelis Financial Management</strong> para <em>{orgName}</em> ha sido creada.</p>
            <p>Haz clic en el enlace a continuación para establecer tu contraseña y acceder al sistema:</p>
            <p><a href="{setPasswordLink}" style="background:#c8a44a;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:bold;">Establecer contraseña</a></p>
            <p>Este enlace expira en 48 horas.</p>
            <p>Si no esperabas este correo, ignóralo.</p>
            <br/><p>— Fidelis Financial Management</p>
            """;

        await SendAsync(toEmail, toName, subject, body, cancellationToken);
    }

    public async Task SendTenantWelcomeAsync(string toEmail, string orgName, string loginUrl, CancellationToken cancellationToken = default)
    {
        // orgName comes from user-entered signup data — encode it so it can't inject HTML.
        var safeOrg = WebUtility.HtmlEncode(orgName);
        var safeEmail = WebUtility.HtmlEncode(toEmail);
        var subject = $"Tu cuenta de Fidelis está lista — {orgName}";
        var body = $"""
            <p>¡Bienvenido a <strong>Fidelis Financial Management</strong>!</p>
            <p>La cuenta de <em>{safeOrg}</em> ya está activa y lista para usarse.</p>
            <p>Ingresa con el correo <strong>{safeEmail}</strong> y la contraseña que elegiste al registrarte:</p>
            <p><a href="{loginUrl}" style="background:#c8a44a;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:bold;">Iniciar sesión</a></p>
            <p><strong>Primeros pasos sugeridos:</strong></p>
            <ol>
                <li>Registra tu primer ingreso (diezmo u ofrenda) desde <em>Ingresos</em>.</li>
                <li>Invita a tu tesorero o auditor desde <em>Usuarios</em>.</li>
                <li>Revisa tus reportes financieros en <em>Reportes</em>.</li>
            </ol>
            <p>Si necesitas ayuda, escríbenos a soporte@fidelisfm.com.</p>
            <br/><p>— Equipo de Fidelis Financial Management</p>
            """;

        await SendAsync(toEmail, orgName, subject, body, cancellationToken);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetLink, CancellationToken cancellationToken = default)
    {
        var subject = "Restablecer contraseña — Fidelis Financial Management";
        var body = $"""
            <p>Hola {toName},</p>
            <p>Recibiste este correo porque se solicitó un restablecimiento de contraseña para tu cuenta.</p>
            <p>Haz clic en el enlace a continuación para establecer una nueva contraseña:</p>
            <p><a href="{resetLink}" style="background:#c8a44a;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:bold;">Restablecer contraseña</a></p>
            <p>Este enlace expira en 24 horas. Si no solicitaste un restablecimiento, ignora este correo.</p>
            <br/><p>— Fidelis Financial Management</p>
            """;

        await SendAsync(toEmail, toName, subject, body, cancellationToken);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var host = configuration["Email:SmtpHost"];
        var fromAddress = configuration["Email:FromAddress"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
        {
            logger.LogWarning("Email not configured (Email:SmtpHost or Email:FromAddress missing). Skipping send to {Email}.", toEmail);
            return;
        }

        try
        {
            var port = int.TryParse(configuration["Email:SmtpPort"], out var p) ? p : 587;
            var user = configuration["Email:SmtpUser"];
            var password = configuration["Email:SmtpPassword"];
            var fromName = configuration["Email:FromName"] ?? "Fidelis Financial Management";
            var enableSsl = !string.Equals(configuration["Email:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            {
                client.Credentials = new NetworkCredential(user, password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
        }
    }
}
