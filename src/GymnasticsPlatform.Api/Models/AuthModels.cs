namespace GymnasticsPlatform.Api.Models;

// Registration
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName);

public sealed record RegisterResponse(
    string Message,
    bool RequiresEmailVerification);

// Login
public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType,
    UserInfo User);

// Token Refresh
public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType);

// Password Reset
public sealed record ForgotPasswordRequest(string Email);

public sealed record ForgotPasswordResponse(string Message);

// User Info
public sealed record UserInfo(
    string Email,
    string FullName,
    bool OnboardingCompleted);
