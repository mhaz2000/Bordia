using System.Reflection;
using BuildingBlocks.Domain.Base;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Abstract base DbContext with soft-delete and auditing support.
/// All service-specific DbContexts should inherit from this class.
/// </summary>
public abstract class AppDbContext : DbContext
{
    protected AppDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the name of the current service.
    /// </summary>
    public abstract string ServiceName { get; }

    /// <inheritdoc />
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Configures the model, applies global query filters for soft delete,
    /// and configures the outbox table. Table/column naming is snake_case via
    /// <c>UseSnakeCaseNamingConvention()</c> set during options configuration.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply global soft-delete query filter on all AuditableEntity types.
        // We build the filter via a generic helper so the lambda expression has
        // a compile-time-known entity type (EF requires this to translate the filter).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ConfigureSoftDeleteFilter),
                        BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(this, [modelBuilder]);
            }
        }

        // Configure the outbox message table.
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(512);
            entity.Property(e => e.Payload).HasColumnType("text");
            entity.Property(e => e.Error).HasMaxLength(1024);
            entity.HasIndex(e => e.ProcessedOn);
        });
    }

    private static void ConfigureSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditableEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => !e.IsDeleted);
    }

    /// <summary>
    /// Saves changes with automatic auditing and soft-delete handling.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleAuditing();
        HandleSoftDelete();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void HandleAuditing()
    {
        var entries = ChangeTracker.Entries<AuditableEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private void HandleSoftDelete()
    {
        var entries = ChangeTracker.Entries<AuditableEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.SoftDelete();
            }
        }
    }
}
