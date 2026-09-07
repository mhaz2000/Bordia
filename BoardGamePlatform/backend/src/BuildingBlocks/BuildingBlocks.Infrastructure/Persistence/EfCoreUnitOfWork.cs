using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// A thin transaction-boundary wrapper that forwards to the service's <see cref="AppDbContext"/>
/// (see the "IUnitOfWork kept as a thin SaveChangesAsync wrapper" Critical Decision).
/// It is not a repository wrapper or persistence facade.
/// </summary>
/// <typeparam name="TDbContext">The service-specific DbContext type.</typeparam>
public class EfCoreUnitOfWork<TDbContext> : IUnitOfWork
    where TDbContext : AppDbContext
{
    private readonly TDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreUnitOfWork{TDbContext}"/> class.
    /// </summary>
    ///
    public EfCoreUnitOfWork(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
