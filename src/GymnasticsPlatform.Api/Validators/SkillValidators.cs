using FluentValidation;
using Training.Domain.Enums;

namespace GymnasticsPlatform.Api.Validators;

public sealed record CreateSkillRequest(
    string Title,
    string Description,
    int EffectivenessRating,
    IReadOnlyList<GymnasticSection> Sections,
    string? ImageUrl);

public sealed record UpdateSkillRequest(
    string Title,
    string Description,
    int EffectivenessRating,
    IReadOnlyList<GymnasticSection> Sections,
    string? ImageUrl);

public sealed record SearchSkillsRequest(
    string Query,
    int? MaxResults,
    GymnasticSection? Section);

public sealed class CreateSkillRequestValidator : AbstractValidator<CreateSkillRequest>
{
    public CreateSkillRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required")
            .MaximumLength(2000)
            .WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.EffectivenessRating)
            .InclusiveBetween(1, 5)
            .WithMessage("Effectiveness rating must be between 1 and 5");

        RuleFor(x => x.Sections)
            .NotEmpty()
            .WithMessage("At least one gymnastics section is required");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .WithMessage("Image URL must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));
    }
}

public sealed class UpdateSkillRequestValidator : AbstractValidator<UpdateSkillRequest>
{
    public UpdateSkillRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required")
            .MaximumLength(2000)
            .WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.EffectivenessRating)
            .InclusiveBetween(1, 5)
            .WithMessage("Effectiveness rating must be between 1 and 5");

        RuleFor(x => x.Sections)
            .NotEmpty()
            .WithMessage("At least one gymnastics section is required");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .WithMessage("Image URL must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));
    }
}

public sealed class SearchSkillsRequestValidator : AbstractValidator<SearchSkillsRequest>
{
    public SearchSkillsRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Search query is required")
            .MinimumLength(3)
            .WithMessage("Search query must be at least 3 characters");

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(1, 50)
            .WithMessage("Max results must be between 1 and 50")
            .When(x => x.MaxResults.HasValue);
    }
}
