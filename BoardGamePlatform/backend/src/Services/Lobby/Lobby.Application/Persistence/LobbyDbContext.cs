using BuildingBlocks.Infrastructure.Persistence;
using Lobby.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Persistence;

/// <summary>
/// Lobby service database context.
/// Hosted in the Application layer so MediatR handlers can access the database directly
/// (see the "No Repository pattern; direct DbContext access" decision).
/// </summary>
public class LobbyDbContext : AppDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyDbContext"/> class.
    /// </summary>
    public LobbyDbContext(DbContextOptions<LobbyDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public override string ServiceName => "Lobby";

    /// <summary>
    /// Lobby rooms.
    /// </summary>
    public DbSet<Room> Rooms => Set<Room>();

    /// <summary>
    /// Players inside rooms.
    /// </summary>
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();

    /// <summary>
    /// Game-specific room settings.
    /// </summary>
    public DbSet<RoomSettings> RoomSettings => Set<RoomSettings>();

    /// <summary>
    /// Configures the lobby entities.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("rooms");

            entity.HasMany(r => r.Players)
                .WithOne()
                .HasForeignKey(p => p.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Settings)
                .WithOne()
                .HasForeignKey<RoomSettings>(s => s.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => r.RoomCode).IsUnique().HasFilter("\"is_deleted\" = false");
        });

        modelBuilder.Entity<RoomPlayer>(entity =>
        {
            entity.ToTable("room_players");
            entity.HasIndex(p => new { p.RoomId, p.UserId }).IsUnique();
        });

        modelBuilder.Entity<RoomSettings>(entity =>
        {
            entity.ToTable("room_settings");
        });
    }
}