namespace BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested entity is not found.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="NotFoundException"/> with the entity name and key.
    /// </summary>
    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    /// <summary>
    /// Initializes a new <see cref="NotFoundException"/> with a custom message.
    /// </summary>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// The name of the entity type that was not found.
    /// </summary>
    public string? EntityName { get; }

    /// <summary>
    /// The key that was used to look up the entity.
    /// </summary>
    public object? Key { get; }
}
