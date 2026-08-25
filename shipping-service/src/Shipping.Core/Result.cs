namespace Shipping.Core;

public enum ErrorCode
{
    None = 0,

    /// <summary>Caller asked for something that does not exist.</summary>
    NotFound,

    /// <summary>Caller's input is malformed or breaks a business rule.</summary>
    Invalid,

    /// <summary>Caller's input clashes with existing state, e.g. a duplicate name.</summary>
    Conflict,
}

/// <summary>
/// Outcome of an operation that can fail in expected ways. Expected failures are
/// values, not exceptions, so callers (HTTP or CLI) can map them deliberately.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, ErrorCode error, string? message)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
        Message = message;
    }

    public bool IsSuccess { get; }

    public ErrorCode Error { get; }

    public string? Message { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"No value: the operation failed with {Error}.");

    public static Result<T> Success(T value) => new(true, value, ErrorCode.None, null);

    public static Result<T> NotFound(string message) => new(false, default, ErrorCode.NotFound, message);

    public static Result<T> Invalid(string message) => new(false, default, ErrorCode.Invalid, message);

    public static Result<T> Conflict(string message) => new(false, default, ErrorCode.Conflict, message);
}

/// <summary>Non-generic companion for operations that return nothing meaningful.</summary>
public sealed class Result
{
    private Result(bool isSuccess, ErrorCode error, string? message)
    {
        IsSuccess = isSuccess;
        Error = error;
        Message = message;
    }

    public bool IsSuccess { get; }

    public ErrorCode Error { get; }

    public string? Message { get; }

    public static Result Success() => new(true, ErrorCode.None, null);

    public static Result NotFound(string message) => new(false, ErrorCode.NotFound, message);

    public static Result Invalid(string message) => new(false, ErrorCode.Invalid, message);

    public static Result Conflict(string message) => new(false, ErrorCode.Conflict, message);
}
