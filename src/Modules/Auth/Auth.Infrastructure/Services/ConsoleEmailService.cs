using Auth.Application.Services;
using Auth.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Services;

public sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendClubInviteAsync(
        string toEmail,
        string clubName,
        string inviteCode,
        string inviteUrl,
        InviteType inviteType,
        CancellationToken ct)
    {
        var roleLabel = inviteType == InviteType.Coach ? "coach" : "gymnast";

        logger.LogInformation(
            """

            ═══════════════════════════════════════════════════════════════
            📧 EMAIL SENT (Development Mode - Console Only)
            ═══════════════════════════════════════════════════════════════
            To: {Email}
            Subject: You're invited to join {ClubName} as a {Role}

            Invite Code: {InviteCode}
            Invite URL: {InviteUrl}

            This email would be sent via Resend in production.
            ═══════════════════════════════════════════════════════════════

            """,
            toEmail, clubName, roleLabel, inviteCode, inviteUrl);

        return Task.CompletedTask;
    }
}
