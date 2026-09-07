namespace UNO.Models;

/// <summary>
/// Per-game turn timer configuration for UNO sessions.
/// </summary>
public class UnoTurnTimerConfig
{
    /// <summary>Base allowance granted every turn, in seconds.</summary>
    public double BaseTurnSeconds { get; set; } = 30;

    /// <summary>Maximum banked time a player can carry over, in seconds.</summary>
    public double MaxBankSeconds { get; set; } = 120;

    /// <summary>Maximum allowed overshoot before the next turn is shortened, in seconds.</summary>
    public double MaxOverrunSeconds { get; set; } = 15;

    /// <summary>Skipped turns after which a player is treated as AFK and removed.</summary>
    public int MaxAfkTurns { get; set; } = 3;
}

/// <summary>
/// Per-player turn timer accounting.
/// </summary>
public class UnoPlayerTimer
{
    /// <summary>Time banked from fast turns, capped at <see cref="UnoTurnTimerConfig.MaxBankSeconds"/>.</summary>
    public double BankSeconds { get; set; }

    /// <summary>Seconds owed from overruns that the bank could not cover.</summary>
    public double DeferredPenaltySeconds { get; set; }

    /// <summary>Consecutive turns skipped by timeout; resets after a played turn.</summary>
    public int ConsecutiveTimeouts { get; set; }
}