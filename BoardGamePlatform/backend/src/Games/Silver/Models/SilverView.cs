namespace Silver.Models;

/// <summary>
/// A village card as it appears in a player view. <see cref="Value"/> is null
/// when the viewing player is not entitled to know it (face-down card they have
/// never seen); clients render those as card backs and must never infer values.
/// </summary>
public class SilverViewCard
{
    /// <summary>The card instance id (stable, lets clients track cards across moves).</summary>
    public Guid Id { get; set; }

    /// <summary>The werewolf count, or null when hidden from the viewer.</summary>
    public int? Value { get; set; }

    /// <summary>Whether the card is face up (public knowledge).</summary>
    public bool FaceUp { get; set; }

    /// <summary>Whether the card is protected by a Guard or the Silver Amulet (cannot be moved/viewed by outsiders).</summary>
    public bool Protected { get; set; }

    /// <summary>Whether the protection on this card is the Silver Amulet (also blocks the owner).</summary>
    public bool AmuletProtected { get; set; }

    /// <summary>The id of the Guard card covering this card, when one does (public: the cards sit on the table).</summary>
    public Guid? GuardedByCardId { get; set; }
}

/// <summary>
/// The player-visible projection of a Silver game: public information plus the
/// viewer's own private information and knowledge. Produced by
/// <c>SilverGame.GetPlayerView</c>; the authoritative <see cref="SilverState"/>
/// (deck order, removed cards, other players' hidden cards, other players'
/// knowledge) never reaches clients.
/// </summary>
public class SilverView
{
    /// <summary>The current round, 1-4.</summary>
    public int Round { get; set; }

    /// <summary>Index of the player whose turn it is.</summary>
    public int CurrentPlayerIndex { get; set; }

    /// <summary>The current turn sub-phase (public: drawing is observable).</summary>
    public TurnPhase Phase { get; set; }

    /// <summary>Each player's village in seat order, projected per the viewer's entitlements.</summary>
    public List<List<SilverViewCard>> Villages { get; set; } = new();

    /// <summary>How many cards remain in the draw deck (the order is never exposed).</summary>
    public int DeckSize { get; set; }

    /// <summary>How many cards are removed from the game this round (values never exposed).</summary>
    public int RemovedCount { get; set; }

    /// <summary>The top card of the discard pile (public), or null when empty.</summary>
    public SilverViewCard? DiscardTop { get; set; }

    /// <summary>How many cards are buried under the discard top.</summary>
    public int DiscardSize { get; set; }

    /// <summary>
    /// The full discard pile, bottom to top. Every discarded card was face up
    /// when discarded (public information), so values are always visible here.
    /// Required for the Master's pick-any-discard-card ability.
    /// </summary>
    public List<SilverViewCard> DiscardPile { get; set; } = new();

    /// <summary>Cards revealed face up beside the deck by face-up Squires (public, takeable instead of drawing).</summary>
    public List<SilverViewCard> Display { get; set; } = new();

    /// <summary>Cards drawn this turn and not yet resolved; values visible only to the drawing player.</summary>
    public List<SilverViewCard> PendingDraw { get; set; } = new();

    /// <summary>Where the pending card came from ("Deck", "Discard" or "Display").</summary>
    public DrawSource PendingSource { get; set; }

    /// <summary>
    /// True while the Witch player has peeked at the deck top and must still
    /// choose the exchange or the decline (public: the whole table sees the Witch looking).
    /// </summary>
    public bool WitchPeekPending { get; set; }

    /// <summary>
    /// The value of the peeked deck top card, visible only to the Witch player
    /// (derived from their knowledge; other viewers get null).
    /// </summary>
    public int? WitchPeekedValue { get; set; }

    /// <summary>
    /// Seat that must answer the Revealer's prompt by choosing one of their
    /// own face-down cards to reveal, while that choice is pending (public).
    /// </summary>
    public int? RevealerChooserSeat { get; set; }

    /// <summary>Seat of the player who called a census, when the round is in its final turns.</summary>
    public int? CensusCallerIndex { get; set; }

    /// <summary>How many final turns remain after a census call.</summary>
    public int RemainingCensusTurns { get; set; }

    /// <summary>Seat of the Silver Amulet holder (assigned to the starting player in round 1).</summary>
    public int? AmuletHolderIndex { get; set; }

    /// <summary>The card instance the amulet is placed on this round, when placed.</summary>
    public Guid? AmuletPlacedCardId { get; set; }

    /// <summary>Whether the holder may still place the amulet this round.</summary>
    public bool AmuletPlaceable { get; set; }

    /// <summary>Cumulative scores per seat across completed rounds (public after each scoring).</summary>
    public List<int> CumulativeScores { get; set; } = new();

    /// <summary>The per-seat scores of the most recently completed round.</summary>
    public List<int>? LastRoundScores { get; set; }

    /// <summary>The seat that called the census in the most recently completed round.</summary>
    public int? LastRoundCensusCallerIndex { get; set; }

    /// <summary>Seats eliminated by AFK timeouts; their villages are still scored.</summary>
    public List<int> EliminatedPlayerIndexes { get; set; } = new();

    /// <summary>How many of the two per-round peeks the VIEWER has used (peeks are private).</summary>
    public int ViewerPeeksUsed { get; set; }

    /// <summary>Whether the viewer is the current player (convenience for the UI).</summary>
    public bool ViewerIsCurrentPlayer { get; set; }

    /// <summary>
    /// Ability uses already spent this turn (public: ability use is observable
    /// at the table). Entries are ability names or "Ability:{cardId}" for
    /// per-card rights (Enchanter / Guard).
    /// </summary>
    public List<string> AbilitiesUsedThisTurn { get; set; } = new();

    /// <summary>
    /// Whether an ability was used or the amulet was placed this turn, which
    /// blocks calling a census (public: observable at the table).
    /// </summary>
    public bool ActedThisTurn { get; set; }

    /// <summary>Persistent public-safe event log.</summary>
    public List<string> EventLog { get; set; } = new();

    /// <summary>Turn timer configuration (public; the client derives the soft deadline).</summary>
    public SilverTurnTimerConfig? TimerConfig { get; set; }

    /// <summary>Per-player timer accounting, indexed by seat.</summary>
    public List<SilverPlayerTimer> PlayerTimers { get; set; } = new();

    /// <summary>UTC moment the current turn started.</summary>
    public DateTime TurnStartUtc { get; set; }
}
