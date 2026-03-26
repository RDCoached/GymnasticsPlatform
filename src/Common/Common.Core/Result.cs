namespace Common.Core;

public sealed record Result
{
    public bool IsSuccess { get; init; }
    public ErrorType? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }

    private Result(bool isSuccess, ErrorType? errorType, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(ErrorType errorType, string message) => new(false, errorType, message);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(ErrorType errorType, string message) => Result<T>.Failure(errorType, message);
}

public sealed record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public ErrorType? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }

    private Result(bool isSuccess, T? value, ErrorType? errorType, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(ErrorType errorType, string message) => new(false, default, errorType, message);
}

public enum ErrorType
{
    NotFound,
    Validation,
    Conflict,
    Unauthorized,
    Forbidden,
    Internal
}
