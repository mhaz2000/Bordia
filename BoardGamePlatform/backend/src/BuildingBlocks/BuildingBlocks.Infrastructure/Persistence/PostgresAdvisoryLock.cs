using Npgsql;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL session-scoped advisory lock used to elect a single background
/// worker (outbox processor, timeout sweep, cleanup) across service
/// instances: the winner holds an open connection, every other instance's
/// try-acquire returns false until it closes. Releasing happens automatically
/// when the connection is disposed - if a holder crashes, Postgres frees the
/// lock on connection teardown, so another instance takes over.
/// </summary>
public static class PostgresAdvisoryLock
{
    /// <summary>
    /// Attempts to take the named advisory lock. The returned connection
    /// HOLDS the lock - dispose it to release. Null means another worker
    /// currently owns it.
    /// </summary>
    public static async Task<NpgsqlConnection?> TryAcquireAsync(
        string connectionString,
        long key,
        CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@key)";
            command.Parameters.AddWithValue("key", key);
            var acquired = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
            if (!acquired)
            {
                await connection.DisposeAsync();
                return null;
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
