using Auth.Domain.Entities;

namespace Auth.Domain.Tests.Factories;

public static class ClubInviteFactory
{
    public static ClubInvite Valid(
        Guid? clubId = null,
        InviteType? inviteType = null,
        int? maxUses = null,
        DateTimeOffset? expiresAt = null,
        string? description = null,
        TimeProvider? clock = null)
    {
        var timeProvider = clock ?? TimeProvider.System;
        return ClubInvite.Create(
            clubId ?? Guid.NewGuid(),
            inviteType ?? InviteType.Coach,
            maxUses ?? 10,
            expiresAt ?? timeProvider.GetUtcNow().AddDays(7),
            description,
            timeProvider);
    }

    public static ClubInvite Expired(Guid? clubId = null, int? maxUses = null, TimeProvider? clock = null)
    {
        var timeProvider = clock ?? TimeProvider.System;
        return ClubInvite.Create(
            clubId ?? Guid.NewGuid(),
            InviteType.Coach,
            maxUses ?? 10,
            timeProvider.GetUtcNow().AddSeconds(1), // Expires in 1 second
            null,
            timeProvider);
    }

    public static ClubInvite WithMaxUses(int maxUses, Guid? clubId = null)
    {
        return Valid(clubId: clubId, maxUses: maxUses);
    }

    public static ClubInvite ForClub(Guid clubId)
    {
        return Valid(clubId: clubId);
    }
}
