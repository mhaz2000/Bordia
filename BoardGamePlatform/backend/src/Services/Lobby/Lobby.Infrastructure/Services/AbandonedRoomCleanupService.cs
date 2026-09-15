using BuildingBlocks.Application;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using Lobby.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lobby.Infrastructure.Services;

/// <summary>
/// Configuration for the <see cref="AbandonedRoomCleanupService"/>.
/// Bound to the <c>Lobby:RoomCleanup</c> configuration section.
/// </summary>
public class RoomCleanupOptions
{
    /// <summary>How often the cleanup sweep runs, in seconds.</summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>Waiting rooms older than this (in minutes) are closed as abandoned.</summary>
    public int AbandonedRoomMinutes { get; set; } = 60;

    /// <summary>Closed rooms older than this (in minutes) are purged (soft-delete).</summary>
    public int ClosedRoomRetentionMinutes { get; set; } = 1440;
}

/// <summary>
/// Background service that closes waiting rooms which have been abandoned and
/// purges long-closed rooms, keeping the rooms table free of dead data.
/// </summary>
public class AbandonedRoomCleanupService : BackgroundService
{
    /// <summary>Advisory-lock key electing the single room-cleanup sweeper.</summary>
    public const long LockKey = 7003;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AbandonedRoomCleanupService> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbandonedRoomCleanupService"/> class.
    /// </summary>
    public AbandonedRoomCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<AbandonedRoomCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Abandoned room cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Abandoned room cleanup sweep encountered an error");
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var options = scope.ServiceProvider.GetRequiredService<IOptions<RoomCleanupOptions>>().Value;
                await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        // Single-sweeper election across replicas.
        await using var lease = await PostgresAdvisoryLock.TryAcquireAsync(
            _configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
            LockKey,
            cancellationToken);
        if (lease is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<RoomCleanupOptions>>().Value;

        var now = DateTime.UtcNow;
        var abandonedCutoff = now.AddMinutes(-options.AbandonedRoomMinutes);
        var retentionCutoff = now.AddMinutes(-options.ClosedRoomRetentionMinutes);

        var abandonedRooms = await db.Rooms
            .Where(r => r.Status == RoomStatus.Waiting && r.CreatedAt < abandonedCutoff)
            .ToListAsync(cancellationToken);

        foreach (var room in abandonedRooms)
        {
            if (room.Status != RoomStatus.Waiting)
            {
                continue;
            }

            room.Close();

            await outbox.AddAsync(new RoomClosed
            {
                RoomId = room.Id
            }, cancellationToken);

            await publisher.Publish(new LobbyRoomChanged
            {
                RoomId = room.Id,
                ChangeType = RoomChangeType.RoomClosed
            }, cancellationToken);

            _logger.LogInformation("Closed abandoned waiting room {RoomId}", room.Id);
        }

        var expiredClosedRooms = await db.Rooms
            .Where(r => r.Status == RoomStatus.Closed && r.CreatedAt < retentionCutoff)
            .ToListAsync(cancellationToken);

        foreach (var room in expiredClosedRooms)
        {
            // The shared audit base converts this into a soft delete.
            db.Rooms.Remove(room);
            _logger.LogInformation("Purged old closed room {RoomId}", room.Id);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}