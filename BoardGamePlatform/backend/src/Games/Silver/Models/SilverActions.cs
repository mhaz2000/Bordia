using System.Text.Json.Serialization;

namespace Silver.Models;

/// <summary>
/// Types of actions a player (or the Game Service, for system actions) can
/// submit in Silver. Silver is draw/replace/ability based; there is no generic
/// PlayCard.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SilverActionType
{
    /// <summary>Initial-peek right: secretly view one of your own face-down village cards (max two per round).</summary>
    PeekVillageCard,

    /// <summary>Draw the top card of the deck (optionally drawing one extra per face-up Trickster in your village).</summary>
    DrawFromDeck,

    /// <summary>Take the top card of the discard pile; replacing village cards with it is mandatory.</summary>
    TakeDiscard,

    /// <summary>Take one of the face-up Squire-displayed cards; replacing village cards with it is mandatory.</summary>
    TakeSquireCard,

    /// <summary>After a Trickster draw, choose which of the drawn cards to keep; the rest return to the top of the deck.</summary>
    ChooseDrawnCard,

    /// <summary>Discard the drawn card; a 5-12 card opens its ability window.</summary>
    DiscardDrawnCard,

    /// <summary>Replace one or more village cards with the drawn card.</summary>
    ExchangeWithDrawn,

    /// <summary>Replace one or more village cards with the taken discard/Squire card (mandatory after TakeDiscard / TakeSquireCard).</summary>
    ExchangeWithDiscard,

    /// <summary>Use a card ability (Enchanter peek or the discarded 5-12 card's ability).</summary>
    UseAbility,

    /// <summary>Decline the pending 5-12 ability; the turn ends.</summary>
    SkipAbility,

    /// <summary>Place or move a face-up Guard from your village onto one of your other village cards.</summary>
    MoveGuard,

    /// <summary>Remove a Guard from the card it protects (the Guard returns to resting in the village).</summary>
    RemoveGuard,

    /// <summary>Revealer follow-up: the TARGET player chooses which of their own face-down cards to reveal.</summary>
    ChooseRevealCard,

    /// <summary>Call a census (requires four or fewer village cards); the round enters its final turns.</summary>
    CallCensus,

    /// <summary>Place the Silver Amulet on one of your village cards (holder right, once per round).</summary>
    PlaceAmulet,

    /// <summary>System action: the current player's turn timed out.</summary>
    TurnTimeout,

    /// <summary>System action: the game's total time limit was reached.</summary>
    GameTimeExpired
}

/// <summary>The abilities a <see cref="SilverActionType.UseAbility"/> action can invoke.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SilverAbility
{
    /// <summary>Value 2: per face-up Enchanter, once each per turn, peek at one of your own face-down cards.</summary>
    Enchanter,

    /// <summary>Value 5: turn one of your own face-down cards face up.</summary>
    Exposer,

    /// <summary>Value 6: the opponent whose village was targeted reveals one of their own face-down cards (they choose it).</summary>
    Revealer,

    /// <summary>Value 7 (Apprentice Seer): secretly peek at up to two of your own face-down cards.</summary>
    ApprenticeSeer,

    /// <summary>Value 8 (Seer): secretly peek at one face-down card in an opponent's village.</summary>
    Seer,

    /// <summary>Value 9 (Beholder): secretly peek at one face-down card in any village.</summary>
    Beholder,

    /// <summary>Value 10: replace one or more of your village cards with any card from the discard pile.</summary>
    Master,

    /// <summary>Value 11: look at the top deck card, then exchange it into a village or return it unseen-position.</summary>
    Witch,

    /// <summary>Value 12: steal a card from an opponent's village and put one of your cards into its place.</summary>
    Robber
}

/// <summary>Payload for <see cref="SilverActionType.PeekVillageCard"/>.</summary>
public record PeekVillageCardPayload
{
    /// <summary>The slot index in the actor's own village to peek at.</summary>
    public int SlotIndex { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.DrawFromDeck"/>.</summary>
public record DrawFromDeckPayload
{
    /// <summary>
    /// How many EXTRA cards to draw using face-up Tricksters (0 = plain draw).
    /// Validated against the number of face-up Tricksters and the deck size.
    /// </summary>
    public int TricksterExtra { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.TakeSquireCard"/>.</summary>
public record TakeSquireCardPayload
{
    /// <summary>Index of the card inside the Squire display area to take.</summary>
    public int DisplayIndex { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.ChooseDrawnCard"/>.</summary>
public record ChooseDrawnCardPayload
{
    /// <summary>The instance id of the drawn card to keep; the rest return to the top of the deck in draw order.</summary>
    public Guid CardId { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.ExchangeWithDrawn"/> / <see cref="SilverActionType.ExchangeWithDiscard"/>.</summary>
public record ExchangePayload
{
    /// <summary>The village slot indexes of the cards to replace (1 card, or 2+ for a matching set).</summary>
    public List<int> Slots { get; init; } = new();

    /// <summary>For multi replacements: which exchanged slot the new card takes (after collapsing).</summary>
    public int PlacementSlot { get; init; }

    /// <summary>For a failed match: place the new card at the end (true) or the start (false) of the village.</summary>
    public bool NewAtEnd { get; init; }

    /// <summary>For a failed set of 3+: place the penalty card at the end (true) or the start (false) of the village.</summary>
    public bool PenaltyAtEnd { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.UseAbility"/>.</summary>
public record UseAbilityPayload
{
    /// <summary>The ability to use.</summary>
    public SilverAbility Ability { get; init; }

    /// <summary>A slot in the actor's own village (Exposer).</summary>
    public int? OwnSlot { get; init; }

    /// <summary>Slots in the actor's own village to replace (Master / Witch own-village multi replacements).</summary>
    public List<int>? OwnSlots { get; init; }

    /// <summary>The target village's seat (Revealer, Seer, Beholder, Witch, Robber).</summary>
    public int? TargetPlayerIndex { get; init; }

    /// <summary>A slot in the target village (Seer, Beholder, Witch, Robber).</summary>
    public int? TargetSlot { get; init; }

    /// <summary>Master only: the index of the discard card to take (0 = bottom of the pile).</summary>
    public int? DiscardIndex { get; init; }

    /// <summary>Apprentice Seer only: one or two own face-down slots to peek at.</summary>
    public List<int>? PeekSlots { get; init; }

    /// <summary>Master/Witch: for multi replacements, which exchanged slot receives the new card.</summary>
    public int? PlacementSlot { get; init; }

    /// <summary>Master/Witch: failed-match placement (end when true, village start when false).</summary>
    public bool NewAtEnd { get; init; }

    /// <summary>Master/Witch: failed 3+ penalty-card placement (end when true, start when false).</summary>
    public bool PenaltyAtEnd { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.MoveGuard"/>.</summary>
public record MoveGuardPayload
{
    /// <summary>Slot of the face-up Guard card in the actor's village.</summary>
    public int GuardSlot { get; init; }

    /// <summary>Slot of the own-village card the Guard moves onto.</summary>
    public int TargetSlot { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.RemoveGuard"/>.</summary>
public record RemoveGuardPayload
{
    /// <summary>Slot of the guarding Guard card to detach.</summary>
    public int GuardSlot { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.ChooseRevealCard"/> (submitted by the Revealer's target).</summary>
public record ChooseRevealCardPayload
{
    /// <summary>Slot of the chooser's own face-down card to reveal.</summary>
    public int SlotIndex { get; init; }
}

/// <summary>Payload for <see cref="SilverActionType.PlaceAmulet"/>.</summary>
public record PlaceAmuletPayload
{
    /// <summary>The slot index in the holder's own village to protect with the amulet.</summary>
    public int SlotIndex { get; init; }
}
