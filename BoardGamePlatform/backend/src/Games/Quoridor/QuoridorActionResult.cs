using Quoridor.Models;

namespace Quoridor;

/// <summary>
/// Internal result of processing a Quoridor action before wrapping into GameResult.
/// </summary>
internal sealed class QuoridorActionResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public object?[] ErrorArgs { get; set; } = Array.Empty<object?>();
    public List<string> Events { get; set; } = new();
    public bool IsTurnAction { get; set; }
    public bool GameEnded { get; set; }
    public bool SharedVictory { get; set; }
    public int WinnerSeat { get; set; } = -1;
}