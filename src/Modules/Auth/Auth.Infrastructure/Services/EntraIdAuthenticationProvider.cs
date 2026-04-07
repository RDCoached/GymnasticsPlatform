using Auth.Application.Services;
using Azure.Core;
using Azure.Identity;
using Common.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Auth.Infrastructure.Services;

/// <summary>
/// Microsoft Entra ID implementation of IAuthenticationProvider.
/// Uses Microsoft Graph API for user management and OAuth 2.0 for authentication.
/// </summary>
public sealed class EntraIdAuthenticationProvider : IAuthenticationProvider
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<EntraIdAuthenticationProvider> _logger;
    private readonly string _extensionAttributeName;
    private readonly string _tenantId;
    private readonly string _apiClientId;

    public EntraIdAuthenticationProvider(
        IConfiguration configuration,
        ILogger<EntraIdAuthenticationProvider> logger)
    {
        _logger = logger;

        // Load Entra ID configuration
        _tenantId = configuration["Authentication:EntraId:TenantId"]
            ?? throw new InvalidOperationException("Authentication:EntraId:TenantId is not configured");

        _apiClientId = configuration["Authentication:EntraId:ApiClientId"]
            ?? throw new InvalidOperationException("Authentication:EntraId:ApiClientId is not configured");

        var clientSecret = configuration["Authentication:EntraId:ApiClientSecret"]
            ?? throw new InvalidOperationException("Authentication:EntraId:ApiClientSecret is not configured");

        var extensionAppId = configuration["Authentication:EntraId:ExtensionAppId"]
            ?? throw new InvalidOperationException("Authentication:EntraId:ExtensionAppId is not configured");

        _extensionAttributeName = configuration["Authentication:EntraId:TenantIdExtensionAttributeName"]
            ?? $"extension_{extensionAppId}_tenant_id";

        // Initialize Graph client with client credentials flow
        var credential = new ClientSecretCredential(
            _tenantId,
            _apiClientId,
            clientSecret);

        _graphClient = new GraphServiceClient(credential);

        _logger.LogInformation(
            "EntraIdAuthenticationProvider initialized. TenantId={TenantId}, ExtensionAttribute={ExtensionAttribute}",
            _tenantId,
            _extensionAttributeName);
    }

    public async Task<Result<string>> CreateUserAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Creating user in Entra ID: Email={Email}, TenantId={TenantId}",
            email,
            tenantId);

        try
        {
            // Split full name into first and last name
            var nameParts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var givenName = nameParts.Length > 0 ? nameParts[0] : fullName;
            var surname = nameParts.Length > 1 ? nameParts[1] : string.Empty;

            var user = new User
            {
                AccountEnabled = true,
                DisplayName = fullName,
                GivenName = givenName,
                Surname = surname,
                MailNickname = email.Split('@')[0], // Use email prefix as nickname
                UserPrincipalName = email,
                Mail = email,
                PasswordProfile = new PasswordProfile
                {
                    ForceChangePasswordNextSignIn = false,
                    Password = password
                },
                AdditionalData = new Dictionary<string, object>
                {
                    { _extensionAttributeName, tenantId.ToString() }
                }
            };

            var createdUser = await _graphClient.Users.PostAsync(user, cancellationToken: ct);

            if (createdUser?.Id is null)
            {
                _logger.LogError("Failed to create user in Entra ID: response was null or missing ID");
                return Result.Failure<string>(ErrorType.Internal, "Failed to create user");
            }

            _logger.LogInformation(
                "Successfully created user in Entra ID: ProviderUserId={ProviderUserId}, Email={Email}",
                createdUser.Id,
                email);

            return Result.Success(createdUser.Id);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 409 || ex.Message.Contains("already exists"))
        {
            _logger.LogWarning(
                ex,
                "User already exists in Entra ID: Email={Email}",
                email);
            return Result.Failure<string>(ErrorType.Conflict, "A user with this email already exists");
        }
        catch (ServiceException ex)
        {
            _logger.LogError(
                ex,
                "Microsoft Graph API error while creating user: Email={Email}, StatusCode={StatusCode}",
                email,
                ex.ResponseStatusCode);
            return Result.Failure<string>(
                ErrorType.Internal,
                $"Failed to create user: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while creating user in Entra ID: Email={Email}",
                email);
            return Result.Failure<string>(ErrorType.Internal, "An unexpected error occurred");
        }
    }

    public async Task<Result<bool>> EmailExistsAsync(
        string email,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Checking if email exists in Entra ID: Email={Email}", email);

        try
        {
            var users = await _graphClient.Users
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"mail eq '{email}' or userPrincipalName eq '{email}'";
                    config.QueryParameters.Select = ["id"];
                    config.QueryParameters.Top = 1;
                }, cancellationToken: ct);

            var exists = users?.Value?.Count > 0;

            _logger.LogDebug(
                "Email existence check completed: Email={Email}, Exists={Exists}",
                email,
                exists);

            return Result.Success(exists);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(
                ex,
                "Microsoft Graph API error while checking email existence: Email={Email}, StatusCode={StatusCode}",
                email,
                ex.ResponseStatusCode);
            return Result.Failure<bool>(
                ErrorType.Internal,
                $"Failed to check email existence: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while checking email existence: Email={Email}",
                email);
            return Result.Failure<bool>(ErrorType.Internal, "An unexpected error occurred");
        }
    }

    public async Task<Result<ProviderUserInfo?>> GetProviderUserInfoAsync(
        string providerUserId,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Retrieving user info from Entra ID: ProviderUserId={ProviderUserId}",
            providerUserId);

        try
        {
            var user = await _graphClient.Users[providerUserId]
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "mail", "displayName", _extensionAttributeName];
                }, cancellationToken: ct);

            if (user is null)
            {
                _logger.LogWarning(
                    "User not found in Entra ID: ProviderUserId={ProviderUserId}",
                    providerUserId);
                return Result.Success<ProviderUserInfo?>(null);
            }

            var userInfo = new ProviderUserInfo(
                user.Id ?? providerUserId,
                user.Mail ?? user.UserPrincipalName ?? string.Empty,
                user.DisplayName);

            _logger.LogDebug(
                "Successfully retrieved user info from Entra ID: ProviderUserId={ProviderUserId}",
                providerUserId);

            return Result.Success<ProviderUserInfo?>(userInfo);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning(
                "User not found in Entra ID: ProviderUserId={ProviderUserId}",
                providerUserId);
            return Result.Success<ProviderUserInfo?>(null);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(
                ex,
                "Microsoft Graph API error while retrieving user info: ProviderUserId={ProviderUserId}, StatusCode={StatusCode}",
                providerUserId,
                ex.ResponseStatusCode);
            return Result.Failure<ProviderUserInfo?>(
                ErrorType.Internal,
                $"Failed to retrieve user info: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while retrieving user info: ProviderUserId={ProviderUserId}",
                providerUserId);
            return Result.Failure<ProviderUserInfo?>(ErrorType.Internal, "An unexpected error occurred");
        }
    }

    public Task<Result<AuthenticationResult>> AuthenticateAsync(
        string email,
        string password,
        string clientId,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "Direct password authentication (ROPC) is not supported in EntraIdAuthenticationProvider. " +
            "Use OAuth 2.0 Authorization Code + PKCE flow from the client. Email={Email}",
            email);

        return Task.FromResult(
            Result.Failure<AuthenticationResult>(
                ErrorType.Internal,
                "Direct password authentication is not supported. Use OAuth 2.0 Authorization Code + PKCE flow."));
    }

    public Task<Result<AuthenticationResult>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "Token refresh should be handled by the client using MSAL. " +
            "Server-side token refresh is not supported in EntraIdAuthenticationProvider.");

        return Task.FromResult(
            Result.Failure<AuthenticationResult>(
                ErrorType.Internal,
                "Token refresh should be handled by the client using MSAL."));
    }

    public async Task<Result> SendVerificationEmailAsync(
        string providerUserId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Sending verification email via Entra ID: ProviderUserId={ProviderUserId}",
            providerUserId);

        try
        {
            // Entra ID sends verification emails automatically when a user is created
            // or when email verification is required. There's no direct API to trigger
            // a verification email for an existing user.
            _logger.LogWarning(
                "Entra ID handles email verification automatically. Manual trigger not supported. " +
                "ProviderUserId={ProviderUserId}",
                providerUserId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during email verification request: ProviderUserId={ProviderUserId}",
                providerUserId);
            return Result.Failure(ErrorType.Internal, "Failed to send verification email");
        }
    }

    public async Task<Result> InitiatePasswordResetAsync(
        string email,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Initiating password reset via Entra ID: Email={Email}",
            email);

        try
        {
            // Entra ID password reset is typically handled through self-service password reset (SSPR)
            // configured in the Azure Portal. The application should redirect users to:
            // https://passwordreset.microsoftonline.com/
            // There's no direct Graph API to trigger a password reset email.

            _logger.LogWarning(
                "Password reset should be handled by redirecting to Entra ID self-service password reset. " +
                "Email={Email}",
                email);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during password reset initiation: Email={Email}",
                email);
            return Result.Failure(ErrorType.Internal, "Failed to initiate password reset");
        }
    }

    public async Task<Result> UpdateUserTenantIdAsync(
        string providerUserId,
        Guid newTenantId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Updating tenant ID via Entra ID: ProviderUserId={ProviderUserId}, NewTenantId={NewTenantId}",
            providerUserId,
            newTenantId);

        try
        {
            var user = new User
            {
                AdditionalData = new Dictionary<string, object>
                {
                    { _extensionAttributeName, newTenantId.ToString() }
                }
            };

            await _graphClient.Users[providerUserId]
                .PatchAsync(user, cancellationToken: ct);

            _logger.LogInformation(
                "Successfully updated tenant ID in Entra ID: ProviderUserId={ProviderUserId}, NewTenantId={NewTenantId}",
                providerUserId,
                newTenantId);

            return Result.Success();
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning(
                "User not found when updating tenant ID: ProviderUserId={ProviderUserId}",
                providerUserId);
            return Result.Failure(ErrorType.NotFound, "User not found");
        }
        catch (ServiceException ex)
        {
            _logger.LogError(
                ex,
                "Microsoft Graph API error while updating tenant ID: ProviderUserId={ProviderUserId}, StatusCode={StatusCode}",
                providerUserId,
                ex.ResponseStatusCode);
            return Result.Failure(
                ErrorType.Internal,
                $"Failed to update tenant ID: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while updating tenant ID: ProviderUserId={ProviderUserId}",
                providerUserId);
            return Result.Failure(ErrorType.Internal, "An unexpected error occurred");
        }
    }
}
