using FluentValidation;
using GymnasticsPlatform.Api.Endpoints;

namespace GymnasticsPlatform.Api.Validators;

public sealed class SendEmailInviteRequestValidator : AbstractValidator<SendEmailInviteRequest>
{
    public SendEmailInviteRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.InviteType)
            .IsInEnum();

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
