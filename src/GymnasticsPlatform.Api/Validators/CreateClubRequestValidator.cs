using FluentValidation;
using GymnasticsPlatform.Api.Endpoints;

namespace GymnasticsPlatform.Api.Validators;

public sealed class CreateClubRequestValidator : AbstractValidator<CreateClubRequest>
{
    public CreateClubRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Club name is required")
            .MinimumLength(2)
            .WithMessage("Club name must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Club name must not exceed 100 characters");
    }
}
