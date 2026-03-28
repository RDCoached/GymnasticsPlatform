using FluentValidation;
using GymnasticsPlatform.Api.Models;

namespace GymnasticsPlatform.Api.Validators;

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required");
    }
}
