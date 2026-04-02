using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;

namespace Auth.Infrastructure.Services;

public sealed class ResendEmailService(
    IResend resend,
    IOptions<EmailSettings> settings,
    ILogger<ResendEmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = settings.Value;

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
        var html = BuildInviteEmailHtml(clubName, inviteCode, inviteUrl, roleLabel);

        var message = new EmailMessage
        {
            From = _settings.FromEmail,
            To = [toEmail],
            Subject = subject,
            HtmlBody = html
        };

        await resend.EmailSendAsync(message, ct);

        logger.LogInformation(
            "Invite email sent to {Email} for club {ClubName} as {Role}",
            toEmail, clubName, roleLabel);
    }

    private static string BuildInviteEmailHtml(
        string clubName,
        string inviteCode,
        string inviteUrl,
        string roleLabel)
    {
        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
                body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }
                .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                .header { background: #AA3BFF; color: white; padding: 30px 20px; text-align: center; border-radius: 8px 8px 0 0; }
                .content { background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }
                .button { display: inline-block; background: #AA3BFF; color: white !important; padding: 14px 32px; text-decoration: none; border-radius: 4px; font-weight: bold; margin: 20px 0; }
                .code-box { background: white; border: 2px dashed #AA3BFF; border-radius: 4px; padding: 20px; text-align: center; margin: 20px 0; }
                .code { font-family: 'Courier New', monospace; font-size: 28px; font-weight: bold; letter-spacing: 3px; color: #AA3BFF; }
                .footer { text-align: center; color: #666; font-size: 14px; margin-top: 20px; }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <h1 style="margin: 0;">You're Invited!</h1>
                </div>
                <div class="content">
                    <p>Hello,</p>
                    <p>You've been invited to join <strong>{{clubName}}</strong> as a <strong>{{roleLabel}}</strong>.</p>
                    <p style="text-align: center;">
                        <a href="{{inviteUrl}}" class="button">Accept Invitation & Register</a>
                    </p>
                    <p>Or copy this code to use when registering:</p>
                    <div class="code-box">
                        <div class="code">{{inviteCode}}</div>
                    </div>
                    <div class="footer">
                        <p>This invitation will expire in 7 days.</p>
                        <p>If you didn't expect this invitation, you can safely ignore this email.</p>
                    </div>
                </div>
            </div>
        </body>
        </html>
        """;
    }
}
