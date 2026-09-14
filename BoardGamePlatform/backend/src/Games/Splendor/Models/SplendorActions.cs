using System.Text.Json.Serialization;

namespace Splendor.Models;

/// <summary>
/// Splendor action types, dispatched by <c>SplendorGame.ProcessAction</c>.
/// Every player action is atomic: the payload carries the complete decision
/// set (tokens taken, tokens returned, payment, noble claim) and the engine
/// resolves the whole turn in one pass (spec §15).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SplendorActionType
{
    /// <summary>Take one token of each of up to three different gem colors (OD-1: forced smaller when supply-limited).</summary>
    TakeThreeGems,

    /// <summary>Take two tokens of the same gem color (requires 4+ of that color in the supply).</summary>
    TakeTwoGems,

    /// <summary>Reserve a face-up market card and take one gold joker when available.</summary>
    ReserveMarketCard,

    /// <summary>Reserve the top card of a tier deck blindly and take one gold joker when available.</summary>
    ReserveDeckCard,

    /// <summary>Purchase a face-up market card.</summary>
    PurchaseMarketCard,

    /// <summary>Purchase one of your own reserved cards.</summary>
    PurchaseReservedCard,

    /// <summary>System action: the current player's turn timed out.</summary>
    TurnTimeout,

    /// <summary>System action: the total game time limit expired.</summary>
    GameTimeExpired
}

/// <summary>A single token-return entry used to satisfy the 10-token limit.</summary>
public sealed class GemReturnPayload
{
    /// <summary>Gem color name (or single letter) — "Gold" is also returnable.</summary>
    public string? Color { get; set; }

    /// <summary>How many tokens of that kind to return (must be positive).</summary>
    public int Count { get; set; } = 1;
}

/// <summary>Payload for <see cref="SplendorActionType.TakeThreeGems"/>.</summary>
public sealed class TakeThreeGemsPayload
{
    /// <summary>The distinct gem colors to take one token of.</summary>
    public List<string>? Colors { get; set; }

    /// <summary>Tokens returned to the supply to satisfy the 10-token limit (required on overflow).</summary>
    public List<GemReturnPayload>? Return { get; set; }

    /// <summary>Noble to receive when two or more are eligible at the end of this turn (carried-over eligibility, spec §10).</summary>
    public string? ClaimNoble { get; set; }
}

/// <summary>Payload for <see cref="SplendorActionType.TakeTwoGems"/>.</summary>
public sealed class TakeTwoGemsPayload
{
    /// <summary>The gem color to take two tokens of.</summary>
    public string? Color { get; set; }

    /// <summary>Tokens returned to the supply to satisfy the 10-token limit (required on overflow).</summary>
    public List<GemReturnPayload>? Return { get; set; }

    /// <summary>Noble claim policy is identical to <see cref="TakeThreeGemsPayload.ClaimNoble"/>.</summary>
    public string? ClaimNoble { get; set; }
}

/// <summary>Payload for <see cref="SplendorActionType.ReserveMarketCard"/>.</summary>
public sealed class ReserveMarketCardPayload
{
    /// <summary>The market card to reserve (its identity is public).</summary>
    public string? CardId { get; set; }

    /// <summary>Tokens returned to the supply to satisfy the 10-token limit (required on overflow from the gold gain).</summary>
    public List<GemReturnPayload>? Return { get; set; }

    /// <summary>Noble claim policy is identical to <see cref="TakeThreeGemsPayload.ClaimNoble"/>.</summary>
    public string? ClaimNoble { get; set; }
}

/// <summary>Payload for <see cref="SplendorActionType.ReserveDeckCard"/>.</summary>
public sealed class ReserveDeckCardPayload
{
    /// <summary>The tier (1-3) whose deck to draw a blind reservation from. The card itself is never named (spec §13).</summary>
    public int Tier { get; set; }

    /// <summary>Tokens returned to the supply to satisfy the 10-token limit (required on overflow from the gold gain).</summary>
    public List<GemReturnPayload>? Return { get; set; }

    /// <summary>Noble claim policy is identical to <see cref="TakeThreeGemsPayload.ClaimNoble"/>.</summary>
    public string? ClaimNoble { get; set; }
}

/// <summary>A purchase payment: exact token counts to spend per color plus gold jokers.</summary>
public sealed class SplendorPaymentPayload
{
    /// <summary>Diamond tokens spent.</summary>
    public int Diamond { get; set; }

    /// <summary>Sapphire tokens spent.</summary>
    public int Sapphire { get; set; }

    /// <summary>Emerald tokens spent.</summary>
    public int Emerald { get; set; }

    /// <summary>Ruby tokens spent.</summary>
    public int Ruby { get; set; }

    /// <summary>Onyx tokens spent.</summary>
    public int Onyx { get; set; }

    /// <summary>Gold jokers spent (cover any color shortfall; never overpaid).</summary>
    public int Gold { get; set; }
}

/// <summary>Payload for <see cref="SplendorActionType.PurchaseMarketCard"/>.</summary>
public sealed class PurchaseMarketCardPayload
{
    /// <summary>The market card to buy.</summary>
    public string? CardId { get; set; }

    /// <summary>Token payment (gold may cover any per-color shortfall; bonuses are automatic discounts).</summary>
    public SplendorPaymentPayload? Payment { get; set; }

    /// <summary>
    /// Required iff two or more nobles are eligible at the end of this turn
    /// (including eligibility carried over from previous turns); ignored when
    /// at most one is eligible, rejected when not among the eligible tiles.
    /// </summary>
    public string? ClaimNoble { get; set; }
}

/// <summary>Payload for <see cref="SplendorActionType.PurchaseReservedCard"/>.</summary>
public sealed class PurchaseReservedCardPayload
{
    /// <summary>Index into the player's reservation list (0-based).</summary>
    public int ReservationIndex { get; set; } = -1;

    /// <summary>Token payment for the formerly reserved card.</summary>
    public SplendorPaymentPayload? Payment { get; set; }

    /// <summary>Noble claim policy is identical to <see cref="PurchaseMarketCardPayload.ClaimNoble"/>.</summary>
    public string? ClaimNoble { get; set; }
}

/// <summary>
/// Builds localized-friendly event log entries as JSON envelopes:
/// {"c":"code","d":{...params}}. The client renders them in the active
/// language through the events.splendor.* dictionary. Entries must stay
/// public-safe: they never contain deck order or a blind reservation's card
/// id before it is purchased (spec §19).
/// </summary>
internal static class SplendorEvent
{
    /// <summary>Serializes an event code with optional parameters into a log entry.</summary>
    public static string Build(string code, object? data = null)
    {
        return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["c"] = code,
            ["d"] = data
        });
    }
}

/// <summary>
/// Internal carrier for the outcome of processing one Splendor action: the
/// per-action processors mutate the state and record their events; the shared
/// end-of-turn pipeline (nobles, threshold, advance) runs in
/// <c>SplendorGame.ProcessAction</c> based on the flags below.
/// </summary>
internal sealed class SplendorActionResult
{
    public bool IsValid { get; set; }

    public string? Error { get; set; }

    public object?[] ErrorArgs { get; set; } = Array.Empty<object?>();

    /// <summary>Public-safe event entries produced by this action (appended to the log at the end).</summary>
    public List<string> Events { get; } = new();

    /// <summary>Whether the action completed a turn (always true for accepted Splendor actions).</summary>
    public bool TurnEnded { get; set; }

    /// <summary>False for system actions (the timeout processor charges the overrun itself).</summary>
    public bool IsTurnAction { get; set; }

    /// <summary>The player's declared noble claim for the end-of-turn step (may be null).</summary>
    public string? ClaimNoble { get; set; }

    /// <summary>True on timeout: with 2+ eligible nobles the engine picks the first in display order (OD-5).</summary>
    public bool AutoNoble { get; set; }

    /// <summary>Set when the action itself ended the game (time expiry, 2-player AFK).</summary>
    public bool GameEnded { get; set; }

    /// <summary>Winning seat when the game ended, or -1 (null winner means shared victory).</summary>
    public int WinnerSeat { get; set; } = -1;

    /// <summary>Whether the winner is unresolved (shared victory).</summary>
    public bool SharedVictory { get; set; }
}
