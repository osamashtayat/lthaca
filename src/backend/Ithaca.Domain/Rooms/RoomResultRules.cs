namespace Ithaca.Domain.Rooms;

/// <summary>One card of a room's shared deck with the votes of the members who count (the present ones).</summary>
/// <param name="Yes">Votes "right".</param>
/// <param name="No">Votes "left".</param>
/// <param name="Votes">All votes, "up" (seen it) included.</param>
public sealed record CardTally(Guid DeckItemId, int Position, double? Popularity, int Yes, int No, int Votes);

/// <summary>What a room decided: the picks are deck item ids, best first.</summary>
public sealed record RoomVerdict(RoomResultKind Kind, IReadOnlyList<Guid> Picks);

/// <summary>
/// How the server decides a room (api-contract.md §6, BE-12). Only present members' votes count (owner, 2026-11-15).
/// <list type="bullet">
/// <item><b>Match:</b> every present member voted "right" on the same card; with several, the earliest in the deck.</item>
/// <item><b>Best compromise:</b> when every present member voted on every card without a match, or when the host
/// finishes. Cards with at least one "right" vote (owner, 2026-11-15), ranked by "right" votes, then fewer "left", then
/// TMDB popularity (unknown last), then deck position; the top <see cref="MaxCompromisePicks"/>.</item>
/// </list>
/// </summary>
public static class RoomResultRules
{
    public const int MaxCompromisePicks = 3;

    /// <summary>The verdict a vote (or a member leaving) brings about, or null while swiping goes on.</summary>
    public static RoomVerdict? Due(IReadOnlyCollection<CardTally> cards, int memberCount) =>
        Match(cards, memberCount) ?? (EveryoneFinished(cards, memberCount) ? Compromise(cards) : null);

    /// <summary>The host ends swiping: the match if there is one, else the best compromise.</summary>
    public static RoomVerdict OnFinish(IReadOnlyCollection<CardTally> cards, int memberCount) =>
        Match(cards, memberCount) ?? Compromise(cards);

    public static RoomVerdict? Match(IReadOnlyCollection<CardTally> cards, int memberCount)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (memberCount < 1)
        {
            return null;
        }

        var match = cards.Where(c => c.Yes >= memberCount).OrderBy(c => c.Position).FirstOrDefault();
        return match is null ? null : new RoomVerdict(RoomResultKind.Match, [match.DeckItemId]);
    }

    /// <summary>Every present member voted on every card (an empty deck or room never counts as finished).</summary>
    public static bool EveryoneFinished(IReadOnlyCollection<CardTally> cards, int memberCount)
    {
        ArgumentNullException.ThrowIfNull(cards);
        return memberCount > 0 && cards.Count > 0 && cards.All(c => c.Votes >= memberCount);
    }

    public static RoomVerdict Compromise(IReadOnlyCollection<CardTally> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var picks = cards
            .Where(c => c.Yes > 0)
            .OrderByDescending(c => c.Yes)
            .ThenBy(c => c.No)
            .ThenByDescending(c => c.Popularity ?? double.NegativeInfinity)
            .ThenBy(c => c.Position)
            .Take(MaxCompromisePicks)
            .Select(c => c.DeckItemId)
            .ToList();
        return new RoomVerdict(RoomResultKind.Compromise, picks);
    }
}
