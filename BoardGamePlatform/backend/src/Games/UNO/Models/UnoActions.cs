using System.Text.Json.Serialization;

namespace UNO.Models;

/// <summary>
/// Types of actions a player can take in UNO.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnoActionType
{
    /// <summary>Play a card from hand.</summary>
    PlayCard,

    /// <summary>Draw a card from the draw pile.</summary>
    DrawCard,

    /// <summary>Call UNO when down to one card.</summary>
    CallUno,

    /// <summary>Challenge a Wild Draw Four play.</summary>
    ChallengeWildDrawFour,

    /// <summary>Accept the draw penalty (don't challenge).</summary>
    AcceptDraw,

    /// <summary>End the turn after a voluntary draw, declining to play the drawn card.</summary>
    Pass,

    /// <summary>System action: the current player's turn timed out.</summary>
    TurnTimeout,

    /// <summary>System action: the game's total time limit was reached.</summary>
    GameTimeExpired
}

/// <summary>
/// Payload for the PlayCard action.
/// </summary>
public record PlayCardPayload
{
    /// <summary>The card to play.</summary>
    public Card Card { get; init; } = new();

    /// <summary>The color chosen when playing a Wild or Wild Draw Four.</summary>
    public CardColor? ChosenColor { get; init; }
}

/// <summary>
/// Payload for the DrawCard action.
/// </summary>
public record DrawCardPayload
{
    /// <summary>Number of cards to draw (for Draw Two / Wild Draw Four).</summary>
    public int Count { get; init; } = 1;
}

/// <summary>
/// Payload for the CallUno action.
/// </summary>
public record CallUnoPayload
{
    // No additional data needed
}

/// <summary>
/// Payload for the ChallengeWildDrawFour action.
/// </summary>
public record ChallengeWildDrawFourPayload
{
    // No additional data needed
}

/// <summary>
/// Payload for the AcceptDraw action.
/// </summary>
public record AcceptDrawPayload
{
    // No additional data needed
}

/// <summary>
/// Payload for the Pass action (declining to play a voluntarily drawn card).
/// </summary>
public record PassPayload
{
    // No additional data needed
}

/// <summary>
/// Result of processing a UNO action.
/// </summary>
public class UnoActionResult
{
    /// <summary>Whether the action was valid.</summary>
    public bool IsValid { get; set; }

    /// <summary>Error message if invalid.</summary>
    public string? Error { get; set; }

    /// <summary>The updated game state.</summary>
    public UnoGameState? NewState { get; set; }

    /// <summary>Events that occurred during action processing.</summary>
    public List<string> Events { get; set; } = new();

    /// <summary>Whether the game ended.</summary>
    public bool GameEnded { get; set; }

    /// <summary>The winner's player index if game ended.</summary>
    public int? WinnerIndex { get; set; }

    /// <summary>The card that was drawn (for DrawCard action).</summary>
    public Card? DrawnCard { get; set; }

    /// <summary>Whether the drawn card can be played immediately.</summary>
    public bool CanPlayDrawnCard { get; set; }

    /// <summary>Whether a Wild Draw Four was challenged.</summary>
    public bool WasChallenged { get; set; }

    /// <summary>Whether the challenge was successful.</summary>
    public bool ChallengeSuccessful { get; set; }

    /// <summary>Creates a successful result.</summary>
    public static UnoActionResult Success(UnoGameState state, List<string>? events = null, bool gameEnded = false, int? winnerIndex = null, Card? drawnCard = null, bool canPlayDrawnCard = false, bool wasChallenged = false, bool challengeSuccessful = false)
        => new() { IsValid = true, NewState = state, Events = events ?? new(), GameEnded = gameEnded, WinnerIndex = winnerIndex, DrawnCard = drawnCard, CanPlayDrawnCard = canPlayDrawnCard, WasChallenged = wasChallenged, ChallengeSuccessful = challengeSuccessful };

    /// <summary>Creates a failure result.</summary>
    public static UnoActionResult Failure(string error)
        => new() { IsValid = false, Error = error };
}