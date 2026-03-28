using FluentValidation;
using GymnasticsPlatform.Api.Models;

namespace GymnasticsPlatform.Api.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .MaximumLength(254)
            .WithMessage("Email must not exceed 254 characters")
            .EmailAddress()
            .WithMessage("Email must be a valid email address");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]")
            .WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[0-9]")
            .WithMessage("Password must contain at least one digit")
            .Matches(@"[^a-zA-Z0-9]")
            .WithMessage("Password must contain at least one special character");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("Full name is required")
            .MinimumLength(2)
            .WithMessage("Full name must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Full name must not exceed 100 characters");
    }
}
