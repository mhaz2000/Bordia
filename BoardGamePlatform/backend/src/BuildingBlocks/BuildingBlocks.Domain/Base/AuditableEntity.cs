namespace BuildingBlocks.Domain.Base;

/// <summary>
/// Base class for auditable entities with soft-delete support.
/// Inherits from <see cref="Entity"/> and adds creation, update, and deletion tracking.
/// </summary>
public abstract class AuditableEntity : Entity
{
    /// <summary>
    /// UTC timestamp when the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the entity was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the entity was soft-deleted.
    /// Null if the entity is not deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Indicates whether the entity has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Marks the entity as soft-deleted.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Restores a soft-deleted entity.
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
