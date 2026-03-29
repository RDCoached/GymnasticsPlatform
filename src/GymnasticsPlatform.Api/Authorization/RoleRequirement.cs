using Auth.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace GymnasticsPlatform.Api.Authorization;

public sealed class RoleRequirement(params Role[] roles) : IAuthorizationRequirement
{
    public IReadOnlyList<Role> Roles { get; } = roles;
}
