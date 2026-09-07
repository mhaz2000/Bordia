namespace BuildingBlocks.Domain.Base;

/// <summary>
/// Base class for all entities with a unique identifier.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public abstract class Entity<TId>
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public TId Id { get; protected set; } = default!;
}

/// <summary>
/// Base class for entities with a <see cref="Guid"/> identifier.
/// </summary>
public abstract class Entity : Entity<Guid>
{
    /// <summary>
    /// Initializes a new entity with a new <see cref="Guid"/> identifier.
    /// </summary>
    protected Entity()
    {
        Id = Guid.NewGuid();
    }
}
