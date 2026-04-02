using Auth.Domain.Entities;

namespace Auth.Application.Services;

public interface IEmailService
{
    Task SendClubInviteAsync(
        string toEmail,
        string clubName,
        string inviteCode,
        string inviteUrl,
        InviteType inviteType,
        CancellationToken ct);
}
