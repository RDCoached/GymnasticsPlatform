using Auth.Domain.Entities;

namespace Auth.Domain.Tests.Factories;

public static class ClubFactory
{
    public static Club Valid(string? name = null, string? ownerUserId = null, TimeProvider? clock = null)
    {
        return Club.Create(
            name ?? "Test Gymnastics Club",
            ownerUserId ?? "test-user-123",
            clock ?? TimeProvider.System);
    }

    public static Club WithName(string name)
    {
        return Valid(name: name);
    }

    public static Club WithOwner(string ownerUserId)
    {
        return Valid(ownerUserId: ownerUserId);
    }
}
