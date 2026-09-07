using BuildingBlocks.Infrastructure.Persistence;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Persistence;

/// <summary>
/// Identity service database context.
/// Hosted in the Application layer so MediatR handlers can access the database directly
/// through this context (see the "No Repository pattern; direct DbContext access" decision).
/// </summary>
public class IdentityDbContext : AppDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityDbContext"/> class.
    /// </summary>
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public override string ServiceName => "Identity";

    /// <summary>
    /// Registered users.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Issued refresh tokens.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Configures the identity entities.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasIndex(t => t.Token).IsUnique();
            entity.HasIndex(t => t.UserId);
        });
    }
}