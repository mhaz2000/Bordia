using Azul.Models;
using GameEngine.Core.Models;

namespace Azul;

/// <summary>
/// Enumeration of the concrete legal choices for a seat (spec §20): each draft
/// names a real source, a color actually present there, and a destination that
/// is either a legal pattern line or the floor sentinel (offered only when the
/// color has no legal line). Engine-level contract consumed by the harness.
/// </summary>
public sealed partial class AzulGame
{
    /// <inheritdoc />
    public IReadOnlyList<GameAction> GetValidActions(GameState state, PlayerId playerId)
    {
        var actions = new List<GameAction>();
        if (state.IsOver)
        {
            return actions;
        }

        if (!state.TryGetString("AzulState", out var json) || json is null)
        {
            return actions;
        }

        var azul = AzulState.FromJson(json);
        Rehydrate(azul, state);

        var seat = IndexOf(state, playerId);
        if (seat < 0 || seat >= azul.PlayerCount || azul.EliminatedSeats.Contains(seat))
        {
            return actions;
        }

        if (seat != azul.CurrentPlayerIndex)
        {
            return actions;
        }

        var seatState = azul.Seats[seat];

        // Factory drafts: one per (factory, color present) x every legal line,
        // or the floor sentinel when the color has no legal line.
        for (var f = 0; f < azul.FactoryCount; f++)
        {
            foreach (var color in azul.Factories[f].Distinct())
            {
                AddDraftActions(actions, playerId, seatState, color,
                    (line) => CreateAction(AzulActionType.DraftFromFactory, playerId, new DraftFromFactoryPayload
                    {
                        FactoryIndex = f,
                        Color = AzulCatalog.ColorName(color),
                        LineIndex = line
                    }));
            }
        }

        // Center drafts: one per color present, plus the standalone marker action.
        foreach (var color in azul.Center.Distinct())
        {
            AddDraftActions(actions, playerId, seatState, color,
                (line) => CreateAction(AzulActionType.DraftFromCenter, playerId, new DraftFromCenterPayload
                {
                    Color = AzulCatalog.ColorName(color),
                    LineIndex = line
                }));
        }

        if (azul.MarkerSeat == -1)
        {
            actions.Add(CreateAction(AzulActionType.TakeFirstPlayer, playerId, new { }));
        }

        return actions;
    }

    private static void AddDraftActions(
        List<GameAction> actions,
        PlayerId playerId,
        AzulSeatState seatState,
        int color,
        Func<int, GameAction> build)
    {
        var anyLegal = false;
        for (var line = 0; line < AzulCatalog.LineCount; line++)
        {
            if (seatState.CanPlace(line, color))
            {
                actions.Add(build(line));
                anyLegal = true;
            }
        }

        if (!anyLegal)
        {
            actions.Add(build(AzulFloorSentinel.Value));
        }
    }
}
