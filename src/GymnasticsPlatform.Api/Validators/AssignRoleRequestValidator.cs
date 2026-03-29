using Auth.Domain.Entities;
using FluentValidation;
using GymnasticsPlatform.Api.Endpoints;

namespace GymnasticsPlatform.Api.Validators;

public sealed class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Invalid role");

        RuleFor(x => x.Role)
            .NotEqual(Role.IndividualAdmin)
            .WithMessage("Cannot assign IndividualAdmin role in club context");
    }
}
