namespace UNO.Models;

/// <summary>
/// Per-game timer configuration for UNO sessions: turn clock and total game duration.
/// </summary>
public class UnoTurnTimerConfig
{
    /// <summary>Base allowance granted every turn, in seconds.</summary>
    public double BaseTurnSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum total turn allowance (base + banked time), in seconds. The bank's
    /// effective capacity is this value minus <see cref="BaseTurnSeconds"/>, so a
    /// single turn can never exceed this ceiling.
    /// </summary>
    public double MaxBankSeconds { get; set; } = 120;

    /// <summary>Maximum allowed overshoot before the next turn is shortened, in seconds.</summary>
    public double MaxOverrunSeconds { get; set; } = 15;

    /// <summary>Skipped turns after which a player is treated as AFK and removed.</summary>
    public int MaxAfkTurns { get; set; } = 3;

    /// <summary>Total allowed game duration before the game is force-finished, in minutes. The player with the fewest cards wins; a tie is a draw.</summary>
    public double TotalGameTimeMinutes { get; set; } = 60;
}

/// <summary>
/// Per-player turn timer accounting.
/// </summary>
public class UnoPlayerTimer
{
    /// <summary>Time banked from fast turns; a turn's total allowance (base + bank) is capped at <see cref="UnoTurnTimerConfig.MaxBankSeconds"/>.</summary>
    public double BankSeconds { get; set; }

    /// <summary>Seconds owed from overruns that the bank could not cover.</summary>
    public double DeferredPenaltySeconds { get; set; }

    /// <summary>Consecutive turns skipped by timeout; resets after a played turn.</summary>
    public int ConsecutiveTimeouts { get; set; }
}