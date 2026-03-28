namespace Auth.Domain.Entities;

public sealed class ClubInvite
{
    public Guid Id { get; private set; }
    public Guid ClubId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int MaxUses { get; private set; }
    public int TimesUsed { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ClubInvite() { }

    public static ClubInvite Create(Guid clubId, int maxUses, DateTimeOffset expiresAt, TimeProvider clock)
    {
        if (maxUses <= 0)
            throw new ArgumentException("Max uses must be greater than zero.", nameof(maxUses));

        if (expiresAt <= clock.GetUtcNow())
            throw new ArgumentException("Expiration date must be in the future.", nameof(expiresAt));

        return new ClubInvite
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            Code = GenerateInviteCode(),
            MaxUses = maxUses,
            TimesUsed = 0,
            ExpiresAt = expiresAt,
            CreatedAt = clock.GetUtcNow()
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

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }
}
