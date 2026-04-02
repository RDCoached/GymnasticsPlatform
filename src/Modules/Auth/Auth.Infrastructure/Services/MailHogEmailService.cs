using System.Net;
using System.Net.Mail;
using Auth.Application.Services;
using Auth.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Services;

/// <summary>
/// Email service for development that sends via MailHog SMTP.
/// MailHog captures emails locally for testing without sending real emails.
/// </summary>
public sealed class MailHogEmailService(ILogger<MailHogEmailService> logger) : IEmailService
{
    public async Task SendClubInviteAsync(
        string toEmail,
        string clubName,
        string inviteCode,
        string inviteUrl,
        InviteType inviteType,
        CancellationToken ct)
    {
        var roleLabel = inviteType == InviteType.Coach ? "coach" : "gymnast";
        var subject = $"You're invited to join {clubName} as a {roleLabel}";
        var htmlBody = BuildInviteEmailHtml(clubName, inviteCode, inviteUrl, roleLabel);

        using var smtpClient = new SmtpClient("localhost", 1025)
        {
            EnableSsl = false,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential("", "") // MailHog doesn't require auth
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress("noreply@gymnastics.local", "Gymnastics Platform"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        await smtpClient.SendMailAsync(mailMessage, ct);

        logger.LogInformation(
            "Development email sent to {Email} for club {ClubName} as {Role} - view at http://localhost:8025",
            toEmail, clubName, roleLabel);
    }

    private static string BuildInviteEmailHtml(
        string clubName,
        string inviteCode,
        string inviteUrl,
        string roleLabel)
    {
        var template = """
        <!DOCTYPE html>
        <html>
        <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background: #f5f5f5;">
            <div style="max-width: 600px; margin: 0 auto; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1);">
                <div style="background: #AA3BFF; color: white; padding: 30px 20px; text-align: center;">
                    <h1 style="margin: 0;">You're Invited!</h1>
                </div>
                <div style="padding: 30px;">
                    <p>Hello,</p>
                    <p>You've been invited to join <strong>{{CLUB_NAME}}</strong> as a <strong>{{ROLE_LABEL}}</strong>.</p>
                    <div style="text-align: center; margin: 30px 0;">
                        <a href="{{INVITE_URL}}" style="display: inline-block; background: #AA3BFF; color: white; padding: 14px 32px; text-decoration: none; border-radius: 4px; font-weight: bold;">
                            Accept Invitation &amp; Register
                        </a>
                    </div>
                    <p>Or copy this code to use when registering:</p>
                    <div style="background: #f9f9f9; border: 2px dashed #AA3BFF; border-radius: 4px; padding: 20px; text-align: center; margin: 20px 0;">
                        <div style="font-family: 'Courier New', monospace; font-size: 28px; font-weight: bold; letter-spacing: 3px; color: #AA3BFF;">
                            {{INVITE_CODE}}
                        </div>
                    </div>
                    <div style="text-align: center; color: #666; font-size: 14px; margin-top: 30px;">
                        <p>This invitation will expire in 7 days.</p>
                        <p>If you didn't expect this invitation, you can safely ignore this email.</p>
                        <p><em>Development Mode - Email captured by MailHog</em></p>
                    </div>
                </div>
            </div>
        </body>
        </html>
        """;

        return template
            .Replace("{{CLUB_NAME}}", clubName)
            .Replace("{{ROLE_LABEL}}", roleLabel)
            .Replace("{{INVITE_URL}}", inviteUrl)
            .Replace("{{INVITE_CODE}}", inviteCode);
    }
}
