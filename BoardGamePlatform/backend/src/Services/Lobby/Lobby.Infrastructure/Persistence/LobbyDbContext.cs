using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Infrastructure.Persistence;

/// <summary>
/// Lobby service database context.
/// </summary>
public class LobbyDbContext : AppDbContext
{
    public LobbyDbContext(DbContextOptions<LobbyDbContext> options)
        : base(options)
    {
    }

    public override string ServiceName => "Lobby";
}
