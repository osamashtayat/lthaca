using System.Globalization;
using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>
/// The reasons for a room's picks (BE-13): a collaborator of <see cref="RoomResultService"/>, not a use-case service. The
/// vote decides the picks and their order (§6); the AI only explains them. Written on the first read of the result and
/// stored on the room (<see cref="Room.ResultReasons"/>), so every member sees the same reasons and a room costs one AI
/// call. Rules:
/// <list type="bullet">
/// <item>The model gets each pick's Wikidata facts and vote counts only: never who voted what, anyone's mood or hard nos.</item>
/// <item>No picks, or no pick with facts → no AI call; the "none" answer is stored.</item>
/// <item>An AI failure → no reasons this time and nothing stored, so a later read tries again.</item>
/// <item>Readers racing to store: the first wins (the room's concurrency token); the others show what it stored.</item>
/// </list>
/// </summary>
public sealed class RoomPickExplainer(IAppDbContext db, PickAdvisor advisor)
{
    /// <summary>A conflict means another reader stored reasons (or the room was closed), so the second try settles it.</summary>
    private const int Attempts = 2;

    /// <summary>One reason (or null) per pick, in pick order: <paramref name="stored"/> when the room has them, else new ones.</summary>
    public async Task<IReadOnlyList<string?>> ReasonsAsync(
        Guid roomId, string?[]? stored, RoomResultKind kind, int memberCount, IReadOnlyList<RoomPickDto> picks, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(picks);
        if (stored is not null)
        {
            return stored;
        }

        var facts = await advisor.FactsAsync(picks.Select(p => p.Film.TmdbId), ct);
        var explained = picks.Where(p => facts.ContainsKey(p.Film.TmdbId))
            .Select(p => new GroupPick(p.Film.TmdbId, p.YesCount, p.NoCount, p.Seen.Count, facts[p.Film.TmdbId]))
            .ToList();
        Dictionary<int, string?> byFilm = [];
        if (explained.Count > 0)
        {
            var input = new ExplainPicksInput(kind == RoomResultKind.Match ? "match" : "compromise", memberCount, explained);
            if (await advisor.ExplainAsync(Audience(memberCount), input, ct) is not { } advised)
            {
                return new string?[picks.Count];
            }

            byFilm = advised;
        }

        return await StoreAsync(roomId, picks.Select(p => byFilm.GetValueOrDefault(p.Film.TmdbId)).ToArray(), ct);
    }

    private static string Audience(int memberCount) =>
        $"a group of {memberCount.ToString(CultureInfo.InvariantCulture)} friends";

    /// <summary>Stores <paramref name="reasons"/> unless another reader stored theirs first; returns what the room keeps.</summary>
    private async Task<string?[]> StoreAsync(Guid roomId, string?[] reasons, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var room = await db.Rooms.SingleAsync(r => r.Id == roomId, ct);
            if (room.ResultReasons is { } existing)
            {
                return existing;
            }

            room.Explain(reasons);
            try
            {
                await db.SaveChangesAsync(ct);
                return reasons;
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e))
            {
                // Another reader stored reasons, or a close changed the room: read it again; on the last try just show ours.
                await db.Rooms.Entry(room).ReloadAsync(ct);
                if (attempt >= Attempts)
                {
                    return room.ResultReasons ?? reasons;
                }
            }
        }
    }
}
