namespace Silver.Models;

/// <summary>
/// Internal outcome of processing a Silver action: validity, error code, the
/// public-safe events to append to the log, and the flow flags the dispatcher
/// needs (whether the turn/round/game ended, whether the action participates
/// in turn-timer accounting, and whether round-end checks should run).
/// </summary>
internal class SilverActionResult
{
    /// <summary>Whether the action was accepted and applied.</summary>
    public bool IsValid { get; init; }

    /// <summary>Stable error code when the action is invalid; null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Alias of <see cref="Error"/> kept for call-site clarity.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Localization arguments for the error code.</summary>
    public object?[] ErrorArgs { get; init; } = Array.Empty<object?>();

    /// <summary>Public-safe event log entries produced by the action.</summary>
    public List<string> Events { get; } = new();

    /// <summary>Whether the actor's turn ended (the next player is now active).</summary>
    public bool TurnEnded { get; set; }

    /// <summary>Whether the round ended as a result of the action.</summary>
    public bool RoundEnded { get; set; }

    /// <summary>Whether the game ended as a result of the action.</summary>
    public bool GameEnded { get; set; }

    /// <summary>The winning seat, when <see cref="GameEnded"/> is true and there is a winner.</summary>
    public int? WinnerIndex { get; set; }

    /// <summary>Whether the action participates in turn-timer accounting.</summary>
    public bool IsTurnAction { get; set; }

    /// <summary>Whether the handler already advanced the player (system timeout path).</summary>
    public bool PlayerAdvanced { get; set; }

    /// <summary>Whether round-end checks (double Villager, deck depletion) should run.</summary>
    public bool CheckRoundEnd { get; set; }
}
