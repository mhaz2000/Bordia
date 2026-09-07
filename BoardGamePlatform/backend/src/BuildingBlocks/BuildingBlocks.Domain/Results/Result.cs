namespace BuildingBlocks.Domain.Results;

/// <summary>
/// Represents the outcome of an operation without a value.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Collection of error messages when the operation failed.
    /// Empty when the operation succeeded.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    protected Result(bool isSuccess, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, []);

    /// <summary>
    /// Creates a failure result with the specified error message.
    /// </summary>
    public static Result Failure(string error) => new(false, [error]);

    /// <summary>
    /// Creates a failure result with the specified error messages.
    /// </summary>
    public static Result Failure(IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Represents the outcome of an operation with a value.
/// </summary>
/// <typeparam name="T">The type of the returned value.</typeparam>
public class Result<T> : Result
{
    /// <summary>
    /// The value returned by a successful operation.
    /// Null when the operation failed.
    /// </summary>
    public T? Value { get; }

    private Result(bool isSuccess, T? value, IReadOnlyList<string> errors)
        : base(isSuccess, errors)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, []);

    /// <summary>
    /// Creates a failure result with the specified error message.
    /// </summary>
    public static new Result<T> Failure(string error) => new(false, default, [error]);

    /// <summary>
    /// Creates a failure result with the specified error messages.
    /// </summary>
    public static new Result<T> Failure(IReadOnlyList<string> errors) => new(false, default, errors);
}
