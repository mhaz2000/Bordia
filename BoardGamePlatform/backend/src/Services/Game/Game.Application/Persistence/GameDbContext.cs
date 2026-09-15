using BuildingBlocks.Infrastructure.Persistence;
using Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.Application.Persistence;

/// <summary>
/// Game service database context.
/// Hosted in the Application layer so MediatR handlers can access the database directly
/// (see the "No Repository pattern; direct DbContext access" decision).
/// </summary>
public class GameDbContext : AppDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GameDbContext"/> class.
    /// </summary>
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public override string ServiceName => "Game";

    /// <summary>
    /// Active and historical game sessions.
    /// </summary>
    public DbSet<GameSession> GameSessions => Set<GameSession>();

    /// <summary>
    /// Player seats inside sessions.
    /// </summary>
    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();

    /// <summary>
    /// Processed player actions.
    /// </summary>
    public DbSet<GameActionLog> GameActionLogs => Set<GameActionLog>();

    /// <summary>
    /// Configures the game entities.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.ToTable("game_sessions");

            entity.HasMany(s => s.Players)
                .WithOne()
                .HasForeignKey(p => p.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(s => s.CurrentStateJson)
                .HasColumnName("current_state_json");

            // Optimistic concurrency: two writers of the same session (player
            // action vs timeout sweep, any instance) cannot both win - the
            // loser gets DbUpdateConcurrencyException and retries/reports 409.
            entity.Property(s => s.Version)
                .HasColumnName("version")
                .IsConcurrencyToken();

            // The timeout sweep only ever touches active sessions with deadlines.
            entity.HasIndex(s => s.NextActionDeadlineUtc)
                .HasFilter("\"status\" = 0 AND \"is_deleted\" = false AND \"next_action_deadline_utc\" IS NOT NULL");
            entity.HasIndex(s => s.GameEndsAtUtc)
                .HasFilter("\"status\" = 0 AND \"is_deleted\" = false AND \"game_ends_at_utc\" IS NOT NULL");
        });

        modelBuilder.Entity<GamePlayer>(entity =>
        {
            entity.ToTable("game_players");
            entity.HasIndex(p => new { p.GameSessionId, p.UserId }).IsUnique();
            // Disconnect handling looks seats up by connection id.
            entity.HasIndex(p => p.ConnectionId);
        });

        modelBuilder.Entity<GameActionLog>(entity =>
        {
            entity.ToTable("game_action_logs");
            entity.HasIndex(a => new { a.GameSessionId, a.SequenceNumber }).IsUnique();
        });
    }
}