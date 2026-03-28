using System.Net;
using System.Net.Http.Json;
using Auth.Application.Services;
using Auth.Infrastructure.Services;
using Common.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Auth.Infrastructure.Tests.Services;

public class KeycloakAdminServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeycloakAdminService> _logger;

    public KeycloakAdminServiceTests()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Keycloak:AdminBaseUrl"] = "http://localhost:8080",
            ["Keycloak:Realm"] = "gymnastics",
            ["Keycloak:AdminUsername"] = "admin",
            ["Keycloak:AdminPassword"] = "admin",
            ["Keycloak:AdminClientId"] = "admin-cli"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _logger = Substitute.For<ILogger<KeycloakAdminService>>();
    }

    public sealed class CreateUserAsyncTests : KeycloakAdminServiceTests
    {
        [Fact]
        public async Task CreateUserAsync_ValidData_ReturnsSuccessWithUserId()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.CreateUserAsync(
                "test@example.com",
                "Test123!",
                "Test User",
                Guid.Parse("00000000-0000-0000-0000-000000000001"));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNullOrEmpty();
            result.Value.Should().MatchRegex("^[a-f0-9-]{36}$"); // UUID format
        }

        [Fact]
        public async Task CreateUserAsync_DuplicateEmail_ReturnsConflictError()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(
                userCreationStatusCode: HttpStatusCode.Conflict);
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.CreateUserAsync(
                "existing@example.com",
                "Test123!",
                "Test User",
                Guid.Parse("00000000-0000-0000-0000-000000000001"));

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.Conflict);
            result.ErrorMessage.Should().Contain("already exists");
        }

        [Fact]
        public async Task CreateUserAsync_InvalidEmail_ReturnsValidationError()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(
                userCreationStatusCode: HttpStatusCode.BadRequest);
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.CreateUserAsync(
                "invalid-email",
                "Test123!",
                "Test User",
                Guid.Parse("00000000-0000-0000-0000-000000000001"));

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.Validation);
        }
    }

    public sealed class AuthenticateAsyncTests : KeycloakAdminServiceTests
    {
        [Fact]
        public async Task AuthenticateAsync_ValidCredentials_ReturnsTokenResponse()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.AuthenticateAsync(
                "test@example.com",
                "Test123!",
                "user-portal");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.AccessToken.Should().NotBeNullOrEmpty();
            result.Value.RefreshToken.Should().NotBeNullOrEmpty();
            result.Value.ExpiresIn.Should().BeGreaterThan(0);
            result.Value.TokenType.Should().Be("Bearer");
        }

        [Fact]
        public async Task AuthenticateAsync_InvalidCredentials_ReturnsUnauthorizedError()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(
                authStatusCode: HttpStatusCode.Unauthorized);
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.AuthenticateAsync(
                "test@example.com",
                "WrongPassword",
                "user-portal");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.Unauthorized);
            result.ErrorMessage.Should().Contain("Invalid credentials");
        }

        [Fact]
        public async Task AuthenticateAsync_UnverifiedEmail_ReturnsUnauthorizedError()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(
                authStatusCode: HttpStatusCode.Unauthorized,
                authErrorType: "email_not_verified");
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.AuthenticateAsync(
                "unverified@example.com",
                "Test123!",
                "user-portal");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.Unauthorized);
            result.ErrorMessage.Should().ContainAny("email not verified", "Email not verified");
        }
    }

    public sealed class RefreshTokenAsyncTests : KeycloakAdminServiceTests
    {
        [Fact]
        public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokenResponse()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.RefreshTokenAsync(
                "valid-refresh-token",
                "user-portal");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.AccessToken.Should().NotBeNullOrEmpty();
            result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RefreshTokenAsync_ExpiredToken_ReturnsUnauthorizedError()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(
                refreshStatusCode: HttpStatusCode.BadRequest);
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.RefreshTokenAsync(
                "expired-refresh-token",
                "user-portal");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.Unauthorized);
        }
    }

    public sealed class InitiatePasswordResetAsyncTests : KeycloakAdminServiceTests
    {
        [Fact]
        public async Task InitiatePasswordResetAsync_ExistingUser_ReturnsSuccess()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.InitiatePasswordResetAsync("test@example.com");

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task InitiatePasswordResetAsync_NonExistentUser_ReturnsSuccessForSecurity()
        {
            // Arrange - Always returns success to prevent email enumeration
            var httpClient = CreateMockHttpClient(
                userSearchNotFound: true);
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.InitiatePasswordResetAsync("nonexistent@example.com");

            // Assert
            result.IsSuccess.Should().BeTrue(); // Security: don't leak user existence
        }
    }

    public sealed class EmailExistsAsyncTests : KeycloakAdminServiceTests
    {
        [Fact]
        public async Task EmailExistsAsync_ExistingEmail_ReturnsTrue()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.EmailExistsAsync("existing@example.com");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
        }

        [Fact]
        public async Task EmailExistsAsync_NonExistentEmail_ReturnsFalse()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(userSearchNotFound: true);
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.EmailExistsAsync("new@example.com");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeFalse();
        }
    }

    public sealed class SendVerificationEmailAsyncTests : KeycloakAdminServiceTests
    {
        [Fact]
        public async Task SendVerificationEmailAsync_ValidUserId_ReturnsSuccess()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.SendVerificationEmailAsync("user-id-123");

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task SendVerificationEmailAsync_InvalidUserId_ReturnsNotFoundError()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(
                verificationEmailStatusCode: HttpStatusCode.NotFound);
            var service = new KeycloakAdminService(httpClient, _configuration, _logger);

            // Act
            var result = await service.SendVerificationEmailAsync("invalid-user-id");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.NotFound);
        }
    }

    private static HttpClient CreateMockHttpClient(
        HttpStatusCode? userCreationStatusCode = null,
        HttpStatusCode? authStatusCode = null,
        string? authErrorType = null,
        HttpStatusCode? refreshStatusCode = null,
        HttpStatusCode? verificationEmailStatusCode = null,
        bool userSearchNotFound = false)
    {
        var handler = new MockHttpMessageHandler(
            userCreationStatusCode ?? HttpStatusCode.Created,
            authStatusCode ?? HttpStatusCode.OK,
            authErrorType,
            refreshStatusCode ?? HttpStatusCode.OK,
            verificationEmailStatusCode ?? HttpStatusCode.NoContent,
            userSearchNotFound);

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
    }

    private sealed class MockHttpMessageHandler(
        HttpStatusCode userCreationStatusCode,
        HttpStatusCode authStatusCode,
        string? authErrorType,
        HttpStatusCode refreshStatusCode,
        HttpStatusCode verificationEmailStatusCode,
        bool userSearchNotFound) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? "";

            // Admin token request (always succeeds for tests)
            if (path.Contains("/realms/master/protocol/openid-connect/token"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "mock-admin-token",
                        expires_in = 3600
                    })
                };
            }

            // User creation
            if (request.Method == HttpMethod.Post && path.Contains("/admin/realms/gymnastics/users"))
            {
                if (userCreationStatusCode == HttpStatusCode.Created)
                {
                    var userId = Guid.NewGuid().ToString();
                    return new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Headers = { Location = new Uri($"http://localhost:8080/admin/realms/gymnastics/users/{userId}") }
                    };
                }

                return new HttpResponseMessage(userCreationStatusCode)
                {
                    Content = JsonContent.Create(new { errorMessage = "User already exists" })
                };
            }

            // Token requests (authentication and refresh)
            if (path.Contains("/realms/gymnastics/protocol/openid-connect/token") &&
                request.Content is FormUrlEncodedContent content)
            {
                var formData = await content.ReadAsStringAsync(cancellationToken);

                // Check if it's a refresh token request
                if (formData.Contains("grant_type=refresh_token"))
                {
                    if (refreshStatusCode == HttpStatusCode.OK)
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = JsonContent.Create(new
                            {
                                access_token = "mock-refreshed-access-token",
                                refresh_token = "mock-refreshed-refresh-token",
                                expires_in = 3600,
                                token_type = "Bearer"
                            })
                        };
                    }

                    return new HttpResponseMessage(refreshStatusCode)
                    {
                        Content = JsonContent.Create(new
                        {
                            error = "invalid_grant",
                            error_description = "Token expired or invalid"
                        })
                    };
                }

                // Password grant authentication
                if (authStatusCode == HttpStatusCode.OK)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            access_token = "mock-access-token",
                            refresh_token = "mock-refresh-token",
                            expires_in = 3600,
                            token_type = "Bearer"
                        })
                    };
                }

                var errorResponse = authErrorType == "email_not_verified"
                    ? new { error = "invalid_grant", error_description = "Account is not fully set up - email not verified" }
                    : new { error = "invalid_grant", error_description = "Invalid credentials" };

                return new HttpResponseMessage(authStatusCode)
                {
                    Content = JsonContent.Create(errorResponse)
                };
            }

            // User search by email
            if (request.Method == HttpMethod.Get && path.Contains("/admin/realms/gymnastics/users") && path.Contains("email="))
            {
                if (userSearchNotFound)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(Array.Empty<object>())
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        new { id = "user-123", email = "test@example.com" }
                    })
                };
            }

            // Send verification email
            if (request.Method == HttpMethod.Put && path.Contains("/execute-actions-email"))
            {
                return new HttpResponseMessage(verificationEmailStatusCode);
            }

            // Default response
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
