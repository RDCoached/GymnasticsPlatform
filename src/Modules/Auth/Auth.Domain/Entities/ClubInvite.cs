namespace Auth.Domain.Entities;

public sealed class ClubInvite
{
    public Guid Id { get; private set; }
    public Guid ClubId { get; private set; }
    public InviteType InviteType { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int MaxUses { get; private set; }
    public int TimesUsed { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? Description { get; private set; }
    public string? Email { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    private ClubInvite() { }

    public static ClubInvite Create(
        Guid clubId,
        InviteType inviteType,
        int maxUses,
        DateTimeOffset expiresAt,
        string? description,
        string? email,
        TimeProvider clock)
    {
        if (maxUses <= 0)
            throw new ArgumentException("Max uses must be greater than zero.", nameof(maxUses));

        if (expiresAt <= clock.GetUtcNow())
            throw new ArgumentException("Expiration date must be in the future.", nameof(expiresAt));

        if (email is not null && maxUses != 1)
            throw new ArgumentException("Email-specific invites must have MaxUses = 1.", nameof(maxUses));

        return new ClubInvite
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            InviteType = inviteType,
            Code = GenerateInviteCode(),
            MaxUses = maxUses,
            TimesUsed = 0,
            ExpiresAt = expiresAt,
            CreatedAt = clock.GetUtcNow(),
            Description = description,
            Email = email,
            SentAt = email is not null ? clock.GetUtcNow() : null
        };
    }

    public void MarkAsUsed(TimeProvider clock)
    {
        if (IsAtMaxUses())
            throw new InvalidOperationException("Invite has reached its maximum number of uses.");

        if (IsExpired(clock.GetUtcNow()))
            throw new InvalidOperationException("Invite has expired.");

        TimesUsed++;
    }

    public bool IsExpired(DateTimeOffset currentTime) => currentTime >= ExpiresAt;

    public bool IsAtMaxUses() => TimesUsed >= MaxUses;

    public bool IsSingleUse() => Email is not null;

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }
}
