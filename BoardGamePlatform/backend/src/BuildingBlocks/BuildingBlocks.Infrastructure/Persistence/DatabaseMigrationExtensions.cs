using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Extension methods for applying EF Core migrations at startup.
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies any pending migrations on the given context.
    /// Used at startup so services self-migrate in Docker and local development.
    /// </summary>
    /// <typeparam name="TDbContext">The service DbContext.</typeparam>
    /// <param name="services">The application service provider.</param>
    public static void MigrateDatabase<TDbContext>(this IServiceProvider services)
        where TDbContext : DbContext
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        dbContext.Database.Migrate();
    }
}