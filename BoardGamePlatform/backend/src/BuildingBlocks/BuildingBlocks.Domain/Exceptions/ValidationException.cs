namespace BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Exception thrown when one or more validation errors occur.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Dictionary of property names and their validation error messages.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Initializes a new <see cref="ValidationException"/> with the specified validation errors.
    /// </summary>
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// <summary>
    /// Initializes a new <see cref="ValidationException"/> with a single error.
    /// </summary>
    public ValidationException(string property, string error)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]> { { property, [error] } };
    }
}
