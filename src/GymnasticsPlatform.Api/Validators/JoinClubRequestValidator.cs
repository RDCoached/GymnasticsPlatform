using FluentValidation;
using GymnasticsPlatform.Api.Endpoints;

namespace GymnasticsPlatform.Api.Validators;

public sealed class JoinClubRequestValidator : AbstractValidator<JoinClubRequest>
{
    public JoinClubRequestValidator()
    {
        RuleFor(x => x.InviteCode)
            .NotEmpty()
            .WithMessage("Invite code is required")
            .Length(6)
            .WithMessage("Invite code must be exactly 6 characters")
            .Matches("^[A-Z0-9]+$")
            .WithMessage("Invite code must contain only uppercase letters and numbers");
    }
}
