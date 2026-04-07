using Auth.Infrastructure.Services;
using Common.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Auth.Infrastructure.Tests.Services;

public class EntraIdAuthenticationProviderTests
{
    private readonly ILogger<EntraIdAuthenticationProvider> _logger;

    public EntraIdAuthenticationProviderTests()
    {
        _logger = Substitute.For<ILogger<EntraIdAuthenticationProvider>>();
    }

    private static IConfiguration CreateValidConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Authentication:EntraId:TenantId"] = "12345678-1234-1234-1234-123456789012",
            ["Authentication:EntraId:ApiClientId"] = "87654321-4321-4321-4321-210987654321",
            ["Authentication:EntraId:ApiClientSecret"] = "test-secret-value",
            ["Authentication:EntraId:Instance"] = "https://login.microsoftonline.com/",
            ["Authentication:EntraId:Audience"] = "api://gymnastics-api",
            ["Authentication:EntraId:ExtensionAppId"] = "abc123def456",
            ["Authentication:EntraId:TenantIdExtensionAttributeName"] = "extension_abc123def456_tenant_id"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    public sealed class ConstructorTests : EntraIdAuthenticationProviderTests
    {
        [Fact]
        public void Constructor_WithValidConfiguration_CreatesInstance()
        {
            // Arrange
            var configuration = CreateValidConfiguration();

            // Act
            var action = () => new EntraIdAuthenticationProvider(configuration, _logger);

            // Assert
            action.Should().NotThrow();
        }

        [Fact]
        public void Constructor_MissingTenantId_ThrowsInvalidOperationException()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                ["Authentication:EntraId:ApiClientId"] = "87654321-4321-4321-4321-210987654321",
                ["Authentication:EntraId:ApiClientSecret"] = "test-secret",
                ["Authentication:EntraId:ExtensionAppId"] = "abc123"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            var action = () => new EntraIdAuthenticationProvider(configuration, _logger);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*TenantId*not configured*");
        }

        [Fact]
        public void Constructor_MissingApiClientId_ThrowsInvalidOperationException()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                ["Authentication:EntraId:TenantId"] = "12345678-1234-1234-1234-123456789012",
                ["Authentication:EntraId:ApiClientSecret"] = "test-secret",
                ["Authentication:EntraId:ExtensionAppId"] = "abc123"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            var action = () => new EntraIdAuthenticationProvider(configuration, _logger);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*ApiClientId*not configured*");
        }

        [Fact]
        public void Constructor_MissingApiClientSecret_ThrowsInvalidOperationException()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                ["Authentication:EntraId:TenantId"] = "12345678-1234-1234-1234-123456789012",
                ["Authentication:EntraId:ApiClientId"] = "87654321-4321-4321-4321-210987654321",
                ["Authentication:EntraId:ExtensionAppId"] = "abc123"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            var action = () => new EntraIdAuthenticationProvider(configuration, _logger);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*ApiClientSecret*not configured*");
        }

        [Fact]
        public void Constructor_MissingExtensionAppId_ThrowsInvalidOperationException()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                ["Authentication:EntraId:TenantId"] = "12345678-1234-1234-1234-123456789012",
                ["Authentication:EntraId:ApiClientId"] = "87654321-4321-4321-4321-210987654321",
                ["Authentication:EntraId:ApiClientSecret"] = "test-secret"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            var action = () => new EntraIdAuthenticationProvider(configuration, _logger);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*ExtensionAppId*not configured*");
        }
    }

    public sealed class AuthenticateAsyncTests : EntraIdAuthenticationProviderTests
    {
        [Fact]
        public async Task AuthenticateAsync_Always_ReturnsNotSupportedError()
        {
            // Arrange
            var configuration = CreateValidConfiguration();
            var provider = new EntraIdAuthenticationProvider(configuration, _logger);

            // Act
            var result = await provider.AuthenticateAsync(
                "test@example.com",
                "password123",
                "client-id");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.Internal);
            result.ErrorMessage.Should().Contain("not supported");
            result.ErrorMessage.Should().Contain("OAuth 2.0");
        }
    }

    public sealed class RefreshTokenAsyncTests : EntraIdAuthenticationProviderTests
    {
        [Fact]
        public async Task RefreshTokenAsync_Always_ReturnsNotSupportedError()
        {
            // Arrange
            var configuration = CreateValidConfiguration();
            var provider = new EntraIdAuthenticationProvider(configuration, _logger);

            // Act
            var result = await provider.RefreshTokenAsync(
                "refresh-token",
                "client-id");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.Internal);
            result.ErrorMessage.Should().Contain("MSAL");
        }
    }

    public sealed class SendVerificationEmailAsyncTests : EntraIdAuthenticationProviderTests
    {
        [Fact]
        public async Task SendVerificationEmailAsync_Always_ReturnsSuccess()
        {
            // Arrange
            var configuration = CreateValidConfiguration();
            var provider = new EntraIdAuthenticationProvider(configuration, _logger);

            // Act
            var result = await provider.SendVerificationEmailAsync("user-id-123");

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
    }

    public sealed class InitiatePasswordResetAsyncTests : EntraIdAuthenticationProviderTests
    {
        [Fact]
        public async Task InitiatePasswordResetAsync_Always_ReturnsSuccess()
        {
            // Arrange
            var configuration = CreateValidConfiguration();
            var provider = new EntraIdAuthenticationProvider(configuration, _logger);

            // Act
            var result = await provider.InitiatePasswordResetAsync("test@example.com");

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
    }
}
