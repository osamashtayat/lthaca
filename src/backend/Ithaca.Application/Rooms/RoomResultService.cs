using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>
/// A room's result (api-contract.md §6, BE-12): the host's <c>finish</c> and reading the result. Votes and leaving decide a
/// room on their own through <see cref="RoomReferee"/>. The picks name who said yes and who has seen it, and only count
/// the noes (A7), among the members present when the room was decided.
/// </summary>
public sealed class RoomResultService(IAppDbContext db, RoomAccess access, RoomReferee referee)
{
    /// <summary>
    /// Host only. While swiping: decides now (the match if there is one, else the best compromise). Once decided: the same
    /// result again. In the lobby or once closed: <c>room.invalid_state</c>.
    /// </summary>
    public async Task<Result<RoomResultDto>> FinishAsync(string code, CancellationToken ct)
    {
        var host = await access.HostAsync(code, ct);
        if (!host.IsSuccess)
        {
            return host.Error;
        }

        var roomId = host.Value.RoomId;
        if (await StatusAsync(roomId, ct) == RoomStatus.Swiping)
        {
            await referee.DecideAsync(roomId, finish: true, ct);
        }

        // Decided here, by a concurrent vote, or earlier; a room closed meanwhile (or in the lobby) has nothing to show.
        return await StatusAsync(roomId, ct) == RoomStatus.Result && await ResultAsync(roomId, ct) is { } result
            ? result
            : RoomErrors.InvalidState;
    }

    /// <summary>Any member, once the room is decided (also after it was closed); before that <c>room.no_result</c>.</summary>
    public async Task<Result<RoomResultDto>> GetResultAsync(string code, CancellationToken ct)
    {
        var membership = await access.MemberAsync(code, ct);
        if (!membership.IsSuccess)
        {
            return membership.Error;
        }

        return await ResultAsync(membership.Value.RoomId, ct) is { } result ? result : RoomErrors.NoResult;
    }

    private Task<RoomStatus> StatusAsync(Guid roomId, CancellationToken ct) =>
        db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => r.Status).SingleAsync(ct);

    /// <summary>The stored result with each pick's film and votes, or null when the room isn't decided.</summary>
    private async Task<RoomResultDto?> ResultAsync(Guid roomId, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking()
            .Where(r => r.Id == roomId)
            .Select(r => new { r.ResultKind, r.DecidedAt, r.ResultPicks, r.ResultMembers })
            .SingleAsync(ct);
        if (room is not { ResultKind: { } kind, DecidedAt: { } decidedAt })
        {
            return null;
        }

        var picks = room.ResultPicks;
        var members = room.ResultMembers;
        var filmOf = await db.DeckItems.AsNoTracking()
            .Where(i => picks.Contains(i.Id))
            .Join(db.Films, i => i.FilmId, f => f.Id, (i, f) => new { i.Id, f.TmdbId })
            .ToDictionaryAsync(x => x.Id, x => x.TmdbId, ct);
        var tmdbIds = filmOf.Values.ToList();
        var films = await db.Films.AsNoTracking().Where(f => tmdbIds.Contains(f.TmdbId)).Select(FilmProjections.Summary).ToDictionaryAsync(f => f.TmdbId, ct);
        var votes = await db.Votes.AsNoTracking()
            .Where(v => picks.Contains(v.DeckItemId) && members.Contains(v.RoomMemberId))
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .Select(v => new { v.DeckItemId, v.RoomMemberId, v.Direction })
            .ToListAsync(ct);

        var dtos = picks
            .Select((itemId, i) =>
            {
                var on = votes.Where(v => v.DeckItemId == itemId).ToList();
                var yes = on.Where(v => v.Direction == SwipeDirection.Right).Select(v => v.RoomMemberId).ToList();
                var seen = on.Where(v => v.Direction == SwipeDirection.Up).Select(v => v.RoomMemberId).ToList();
                var noCount = on.Count(v => v.Direction == SwipeDirection.Left);
                return new RoomPickDto(i + 1, films[filmOf[itemId]], yes.Count, noCount, yes, seen);
            })
            .ToList();
        return new RoomResultDto(kind, dtos, members.Length, decidedAt);
    }
}
