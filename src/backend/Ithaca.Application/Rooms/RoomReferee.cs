using Ithaca.Application.Common;
using Ithaca.Application.Realtime;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>
/// Decides a swiping room by <see cref="RoomResultRules"/> (BE-12): a collaborator of <see cref="RoomDeckService"/> (after
/// each vote), <see cref="RoomService"/> (after a member leaves) and <see cref="RoomResultService"/> (the host's finish),
/// not a use-case service.
/// <para>It runs <b>after</b> the caller's own commit, with its own <c>SaveChangesAsync</c>: two members casting the last
/// "right" votes at the same moment can't see each other's uncommitted vote, but whichever commits last sees both here, so
/// no match is missed. Two deciders racing are settled by the room's concurrency token (<c>xmin</c>): the loser reads the
/// room again, finds it decided, and does nothing, so a room gets exactly one result and one <c>roomMatched</c>.</para>
/// </summary>
public sealed class RoomReferee(IAppDbContext db, RoomReader reader, IRoomEventPublisher events, TimeProvider time)
{
    /// <summary>A conflict means another request decided or closed the room, so the second read always ends it.</summary>
    private const int Attempts = 2;

    /// <summary>
    /// Decides the room when a result is due (a match, or every present member finished the deck), or, with
    /// <paramref name="finish"/>, in any case. Only a swiping room is decided. Publishes <c>roomMatched</c> after its commit.
    /// True when this call decided the room.
    /// </summary>
    public async Task<bool> DecideAsync(Guid roomId, bool finish, CancellationToken ct)
    {
        RoomVerdict verdict;
        for (var attempt = 1; ; attempt++)
        {
            var room = await db.Rooms.SingleAsync(r => r.Id == roomId, ct);
            if (room.Status != RoomStatus.Swiping)
            {
                return false;
            }

            var members = await db.RoomMembers.AsNoTracking().Where(m => m.RoomId == roomId && m.LeftAt == null).Select(m => m.Id).ToListAsync(ct);
            if (members.Count == 0)
            {
                return false;
            }

            var cards = await TalliesAsync(roomId, members, ct);
            var due = finish ? RoomResultRules.OnFinish(cards, members.Count) : RoomResultRules.Due(cards, members.Count);
            if (due is null)
            {
                return false;
            }

            verdict = due;
            room.Decide(verdict, members, time.GetUtcNow());
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e) && attempt < Attempts)
            {
                // Another vote, finish or close changed the room first: read it again (it is decided or closed now).
                await db.Rooms.Entry(room).ReloadAsync(ct);
            }
        }

        var first = verdict.Picks.Count > 0 ? verdict.Picks[0] : (Guid?)null;
        var tmdbId = first is null
            ? null
            : await reader.DeckItemsOf(roomId).Where(i => i.Id == first)
                .Join(db.Films, i => i.FilmId, f => f.Id, (_, f) => (int?)f.TmdbId)
                .SingleAsync(ct);
        await events.PublishAsync(roomId, RoomEvents.RoomMatched, new RoomMatchedEvent(roomId, tmdbId), ct);
        return true;
    }

    /// <summary>Every card of the room's deck with the votes of <paramref name="members"/> only.</summary>
    private Task<List<CardTally>> TalliesAsync(Guid roomId, List<Guid> members, CancellationToken ct)
    {
        var votes = db.Votes.AsNoTracking().Where(v => members.Contains(v.RoomMemberId));
        return reader.DeckItemsOf(roomId)
            .Select(i => new CardTally(
                i.Id,
                i.Position,
                db.Films.Where(f => f.Id == i.FilmId).Select(f => f.Popularity).FirstOrDefault(),
                votes.Count(v => v.DeckItemId == i.Id && v.Direction == SwipeDirection.Right),
                votes.Count(v => v.DeckItemId == i.Id && v.Direction == SwipeDirection.Left),
                votes.Count(v => v.DeckItemId == i.Id)))
            .ToListAsync(ct);
    }
}
