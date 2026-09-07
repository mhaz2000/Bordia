namespace BuildingBlocks.Application;

/// <summary>
/// Represents a transaction boundary for a single unit of work.
/// Implemented as a thin <c>SaveChangesAsync</c> wrapper over the service's DbContext
/// (see the "IUnitOfWork kept as a thin SaveChangesAsync wrapper" Critical Decision).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes within the current transaction boundary.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
