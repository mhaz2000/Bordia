namespace GameEngine.Core.Models;

/// <summary>
/// Identifies a player participating in a game session.
/// Wraps the platform-level user id.
/// </summary>
public readonly record struct PlayerId(Guid UserId)
{
    /// <inheritdoc />
    public override string ToString() => UserId.ToString();
}