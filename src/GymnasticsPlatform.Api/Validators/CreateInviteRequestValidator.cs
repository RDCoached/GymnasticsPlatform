using FluentValidation;
using GymnasticsPlatform.Api.Endpoints;

namespace GymnasticsPlatform.Api.Validators;

public sealed class CreateInviteRequestValidator : AbstractValidator<CreateInviteRequest>
{
    public CreateInviteRequestValidator()
    {
        RuleFor(x => x.InviteType)
            .IsInEnum()
            .WithMessage("Invalid invite type");

        RuleFor(x => x.MaxUses)
            .GreaterThan(0)
            .WithMessage("Max uses must be greater than 0")
            .LessThanOrEqualTo(1000)
            .WithMessage("Max uses cannot exceed 1000");

        RuleFor(x => x.ExpiryDays)
            .GreaterThan(0)
            .WithMessage("Expiry days must be greater than 0")
            .LessThanOrEqualTo(365)
            .WithMessage("Expiry days cannot exceed 365");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description cannot exceed 500 characters")
            .When(x => x.Description is not null);
    }
}
