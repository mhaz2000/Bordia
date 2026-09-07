namespace BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Exception thrown when a conflict occurs (e.g., duplicate resource).
/// </summary>
public class ConflictException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="ConflictException"/> with a custom message.
    /// </summary>
    public ConflictException(string message)
        : base(message)
    {
    }
}
