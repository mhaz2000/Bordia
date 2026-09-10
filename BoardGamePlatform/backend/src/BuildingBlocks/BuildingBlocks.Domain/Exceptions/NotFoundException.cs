namespace BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested entity is not found.
/// Localized via the "common.notFound" catalog entry; the raw entity/key text
/// is embedded as arguments.
/// </summary>
public class NotFoundException : DomainExceptionBase
{
    /// <summary>
    /// Initializes a new <see cref="NotFoundException"/> with the entity name and key.
    /// </summary>
    public NotFoundException(string entityName, object key)
        : base("common.notFound", entityName, key)
    {
        EntityName = entityName;
        Key = key;
    }

    /// <summary>
    /// Initializes a new <see cref="NotFoundException"/> with a raw message-style
    /// code; unknown codes render as the raw text.
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
