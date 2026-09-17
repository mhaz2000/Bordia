using Azul.Models;

namespace Azul;

/// <summary>
/// The end-of-round lifecycle (spec §12-§14): the tiling phase (completed
/// pattern lines tile wall spaces with immediate scoring, floor penalties,
/// cleanup), the game-end trigger, final scoring + winner ladder, and the
/// bag refill. These run deterministically inside <c>ProcessAction</c> after
/// the draft that drains the pool — the tiling phase is never a player phase.
/// </summary>
public sealed partial class AzulGame
{
    private static void EndRound(AzulState azul, AzulActionResult result)
    {
        // Seat processing order: turn order starting at the round's first player.
        var order = RoundSeatOrder(azul);
        foreach (var seat in order)
        {
            var state = azul.Seats[seat];

            // Tiling: completed lines top (5) to bottom (1) — index 4..0.
            for (var line = AzulCatalog.LineCount - 1; line >= 0; line--)
            {
                if (!state.IsLineComplete(line))
                {
                    continue;
                }

                var color = state.LineColor(line);

                // Wall placement: leftmost empty cell of the row this line feeds.
                var col = state.Wall[line].FindIndex(c => c < 0);
                if (col >= 0)
                {
                    state.Wall[line][col] = color;
                    var points = ScoreWallPlacement(state, line, col);
                    state.Score = Math.Max(0, state.Score + points);
                    result.Events.Add(AzulEvent.Build("azul.tileOnWall", new
                    {
                        player = NameOf(azul, seat),
                        color = AzulCatalog.ColorName(color),
                        row = line,
                        points
                    }));
                    result.Events.Add(AzulEvent.Build("azul.scoreUpdated", new
                    {
                        player = NameOf(azul, seat),
                        score = state.Score
                    }));
                }

                // Completed-line leftovers (all but the placed tile) → discard; the line clears.
                var leftovers = state.PatternLines[line].Count - (col >= 0 ? 1 : 0);
                for (var i = 0; i < leftovers; i++)
                {
                    azul.Discard.Add(color);
                }

                state.PatternLines[line].Clear();
            }

            // Floor penalty: each floor tile, plus the marker if held this round.
            var penalty = state.Floor.Count + (azul.MarkerSeat == seat ? 1 : 0);
            if (penalty > 0)
            {
                state.Score = Math.Max(0, state.Score - penalty);
                result.Events.Add(AzulEvent.Build("azul.floorPenalty", new
                {
                    player = NameOf(azul, seat),
                    penalty
                }));
                result.Events.Add(AzulEvent.Build("azul.scoreUpdated", new
                {
                    player = NameOf(azul, seat),
                    score = state.Score
                }));
            }

            // Floor cleanup: tiles to the discard pool.
            azul.Discard.AddRange(state.Floor);
            state.Floor.Clear();
        }

        // The marker holder becomes the next round's starter; the marker returns to the center.
        var newFirst = azul.MarkerSeat != -1 ? azul.MarkerSeat : azul.FirstPlayerSeat;
        azul.MarkerSeat = -1;
        if (azul.EliminatedSeats.Contains(newFirst))
        {
            newFirst = azul.ActiveSeats().DefaultIfEmpty(newFirst).First();
        }

        azul.FirstPlayerSeat = newFirst;

        // Game-end trigger: any wall row completed.
        if (azul.ActiveSeats().Any(s => azul.Seats[s].HasCompletedRow()))
        {
            FinishGame(azul, result);
            return;
        }

        Refill(azul);

        // Degenerate safety (spec I-11): if refill cannot reseed a tile anywhere,
        // no draft is possible — end via the normal ladder instead of stalling.
        if (azul.PoolEmpty() && azul.Bag.Count == 0 && azul.Discard.Count == 0)
        {
            FinishGame(azul, result);
            return;
        }

        azul.RoundNumber++;
        azul.CurrentPlayerIndex = azul.FirstPlayerSeat;
        result.Events.Add(AzulEvent.Build("azul.roundFinished", new
        {
            round = azul.RoundNumber,
            bagCount = azul.Bag.Count
        }));
    }

    /// <summary>Active seats in turn order, starting at the round's first player.</summary>
    private static List<int> RoundSeatOrder(AzulState azul)
    {
        var order = new List<int>();
        var count = azul.PlayerCount;
        for (var step = 0; step < count; step++)
        {
            var seat = (azul.FirstPlayerSeat + step) % count;
            if (azul.IsActive(seat))
            {
                order.Add(seat);
            }
        }

        return order;
    }

    /// <summary>Refills every active factory with up to four tiles, re-bagging the discard when the bag runs short (spec §13d).</summary>
    private static void Refill(AzulState azul)
    {
        var rng = CreateRng(azul, 100 + azul.RoundNumber);
        for (var f = 0; f < azul.FactoryCount; f++)
        {
            if (azul.Bag.Count == 0)
            {
                azul.ReclaimDiscards(rng);
            }

            while (azul.Factories[f].Count < AzulCatalog.TilesPerFactory)
            {
                var tile = azul.DrawFromBag();
                if (tile < 0)
                {
                    break;
                }

                azul.Factories[f].Add(tile);
            }
        }
    }

    /// <summary>
    /// Applies end-game bonuses and picks the winner among active seats
    /// (highest final score → more completed horizontal rows → shared).
    /// </summary>
    private static void FinishGame(AzulState azul, AzulActionResult result)
    {
        var candidates = azul.ActiveSeats().ToList();
        foreach (var seat in candidates)
        {
            var state = azul.Seats[seat];
            state.Score += AzulCatalog.HorizontalBonus * state.CompletedHorizontalRows()
                         + AzulCatalog.VerticalBonus * state.CompletedVerticalColumns()
                         + AzulCatalog.ColorSetBonus * state.CompletedColorSets();
        }

        result.GameEnded = true;
        if (candidates.Count == 0)
        {
            result.WinnerSeat = -1;
            result.SharedVictory = true;
        }
        else
        {
            var best = candidates
                .OrderByDescending(s => azul.Seats[s].Score)
                .ThenByDescending(s => azul.Seats[s].CompletedHorizontalRows())
                .ToList();
            var top = azul.Seats[best[0]];
            var tied = best.Where(s =>
                    azul.Seats[s].Score == top.Score
                    && azul.Seats[s].CompletedHorizontalRows() == top.CompletedHorizontalRows())
                .ToList();
            if (tied.Count == 1)
            {
                result.WinnerSeat = tied[0];
            }
            else
            {
                result.SharedVictory = true;
            }
        }

        result.Events.Add(AzulEvent.Build("azul.gameFinished", new
        {
            winner = result.WinnerSeat >= 0 ? NameOf(azul, result.WinnerSeat) : null,
            scores = azul.Seats.Select(s => s.Score).ToList()
        }));
    }

    /// <summary>Horizontal + vertical contiguous run through a newly placed wall cell, minus one for the double-counted tile (spec §12.1).</summary>
    private static int ScoreWallPlacement(AzulSeatState state, int row, int col)
    {
        var horizontal = 1;
        for (var c = col - 1; c >= 0 && state.Wall[row][c] >= 0; c--)
        {
            horizontal++;
        }

        for (var c = col + 1; c < AzulCatalog.WallSize && state.Wall[row][c] >= 0; c++)
        {
            horizontal++;
        }

        var vertical = 1;
        for (var r = row - 1; r >= 0 && state.Wall[r][col] >= 0; r--)
        {
            vertical++;
        }

        for (var r = row + 1; r < AzulCatalog.WallSize && state.Wall[r][col] >= 0; r++)
        {
            vertical++;
        }

        return horizontal + vertical - 1;
    }
}
