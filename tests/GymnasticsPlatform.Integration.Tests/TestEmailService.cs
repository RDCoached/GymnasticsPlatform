using Auth.Application.Services;
using Auth.Domain.Entities;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class TestEmailService : IEmailService
{
    public List<SentEmail> SentEmails { get; } = [];

    public Task SendClubInviteAsync(
        string toEmail,
        string clubName,
        string inviteCode,
        string inviteUrl,
        InviteType inviteType,
        CancellationToken ct)
    {
        SentEmails.Add(new SentEmail(toEmail, clubName, inviteCode, inviteUrl, inviteType));
        return Task.CompletedTask;
    }
}

public sealed record SentEmail(
    string ToEmail,
    string ClubName,
    string InviteCode,
    string InviteUrl,
    InviteType InviteType);
