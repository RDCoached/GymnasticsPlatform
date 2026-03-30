using Auth.Domain.Entities;
using FluentAssertions;
using FluentValidation.TestHelper;
using GymnasticsPlatform.Api.Endpoints;
using GymnasticsPlatform.Api.Validators;

namespace GymnasticsPlatform.Integration.Tests.Validators;

public sealed class CreateInviteRequestValidatorTests
{
    private readonly CreateInviteRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: 7,
            Description: "Test invite");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidRequestWithoutDescription_PassesValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Gymnast,
            MaxUses: 50,
            ExpiryDays: 30,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_InvalidMaxUses_FailsValidation(int invalidMaxUses)
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: invalidMaxUses,
            ExpiryDays: 7,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxUses)
            .WithErrorMessage("Max uses must be greater than 0");
    }

    [Fact]
    public void Validate_MaxUsesExceedsLimit_FailsValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 1001,
            ExpiryDays: 7,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxUses)
            .WithErrorMessage("Max uses cannot exceed 1000");
    }

    [Fact]
    public void Validate_MaxUsesAtUpperBound_PassesValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 1000,
            ExpiryDays: 7,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.MaxUses);
    }

    [Fact]
    public void Validate_MaxUsesAtLowerBound_PassesValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 1,
            ExpiryDays: 7,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.MaxUses);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(-30)]
    public void Validate_InvalidExpiryDays_FailsValidation(int invalidExpiryDays)
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: invalidExpiryDays,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExpiryDays)
            .WithErrorMessage("Expiry days must be greater than 0");
    }

    [Fact]
    public void Validate_ExpiryDaysExceedsLimit_FailsValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: 366,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExpiryDays)
            .WithErrorMessage("Expiry days cannot exceed 365");
    }

    [Fact]
    public void Validate_ExpiryDaysAtUpperBound_PassesValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: 365,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ExpiryDays);
    }

    [Fact]
    public void Validate_ExpiryDaysAtLowerBound_PassesValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: 1,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ExpiryDays);
    }

    [Fact]
    public void Validate_DescriptionTooLong_FailsValidation()
    {
        // Arrange
        var longDescription = new string('a', 501);
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: 7,
            Description: longDescription);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description cannot exceed 500 characters");
    }

    [Fact]
    public void Validate_DescriptionAtMaxLength_PassesValidation()
    {
        // Arrange
        var maxLengthDescription = new string('a', 500);
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: 7,
            Description: maxLengthDescription);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_EmptyDescription_PassesValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: 7,
            Description: string.Empty);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WhitespaceDescription_PassesValidation()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: 10,
            ExpiryDays: 7,
            Description: "   ");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(InviteType.Coach)]
    [InlineData(InviteType.Gymnast)]
    public void Validate_AllValidInviteTypes_PassValidation(InviteType inviteType)
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: inviteType,
            MaxUses: 10,
            ExpiryDays: 7,
            Description: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InviteType);
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var request = new CreateInviteRequest(
            InviteType: InviteType.Coach,
            MaxUses: -1,
            ExpiryDays: 0,
            Description: new string('a', 501));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxUses);
        result.ShouldHaveValidationErrorFor(x => x.ExpiryDays);
        result.ShouldHaveValidationErrorFor(x => x.Description);
        result.Errors.Should().HaveCount(3);
    }
}
