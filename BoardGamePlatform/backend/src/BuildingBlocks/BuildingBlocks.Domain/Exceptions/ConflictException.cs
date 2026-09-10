namespace BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Exception thrown when a conflict occurs (e.g., duplicate resource).
/// The code localizes the message at the request boundary; unknown codes
/// fall back to the raw text.
/// </summary>
public class ConflictException : DomainExceptionBase
{
    /// <summary>
    /// Initializes a new <see cref="ConflictException"/> with an error code and
    /// optional positional arguments. Legacy string-message call sites keep
    /// working: unknown codes render as the raw text.
    /// </summary>
    public ConflictException(string code, params object?[] args)
        : base(code, args)
    {
    }
}
