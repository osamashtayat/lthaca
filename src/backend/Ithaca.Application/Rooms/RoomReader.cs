using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>
/// How a room reads to its members: the <see cref="RoomDto"/>, its present members (host first, with readiness) and its
/// shared deck. A collaborator of <see cref="RoomService"/> and <see cref="RoomDeckService"/>, not a use-case service.
/// </summary>
public sealed class RoomReader(IAppDbContext db)
{
    /// <summary>
    /// The readiness rule: in a mood room a member is ready once they answered the mood step; in just-swipe and taste rooms
    /// (no lobby step; api-contract.md §12) everyone is ready.
    /// </summary>
    public static bool IsReady(RoomMode mode, bool answeredMood) => mode != RoomMode.Mood || answeredMood;

    /// <summary>The cards of the room's shared deck (none before the host starts).</summary>
    public IQueryable<DeckItem> DeckItemsOf(Guid roomId) =>
        db.DeckItems.AsNoTracking().Where(i => db.Decks.Any(d => d.Id == i.DeckId && d.RoomId == roomId));

    /// <summary>Present members, host first, with their names (a user's from the account, a guest's from the join).</summary>
    public async Task<List<RoomMemberDto>> MembersAsync(Guid roomId, RoomMode mode, CancellationToken ct)
    {
        var users = db.Users;
        var guests = db.Guests.AsNoTracking();
        var moods = db.MoodQueries.AsNoTracking();
        var rows = await RoomAccess.InHostOrder(db.RoomMembers.AsNoTracking().Where(m => m.RoomId == roomId && m.LeftAt == null))
            .Select(m => new
            {
                m.Id,
                IsUser = m.UserId != null,
                m.JoinedAt,
                AnsweredMood = moods.Any(q => q.RoomMemberId == m.Id),
                Name = m.UserId != null
                    ? users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).FirstOrDefault()
                    : guests.Where(g => g.Id == m.GuestId).Select(g => g.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows
            .Select((m, i) => new RoomMemberDto(
                m.Id, m.Name ?? "", m.IsUser ? PrincipalKind.User : PrincipalKind.Guest, IsHost: i == 0, IsReady(mode, m.AnsweredMood), m.JoinedAt))
            .ToList();
    }

    public async Task<RoomDto> RoomDtoAsync(Guid roomId, Guid memberId, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking()
            .Where(r => r.Id == roomId)
            .Select(r => new { r.Id, r.Code, r.Name, r.Mode, r.Status, r.StartsAt, r.CreatedAt })
            .SingleAsync(ct);
        var members = await MembersAsync(roomId, room.Mode, ct);
        var deckSize = await DeckItemsOf(roomId).CountAsync(ct);
        return new RoomDto(
            room.Id, room.Code, room.Name, room.Mode, room.Status, room.StartsAt, room.CreatedAt, deckSize, Room.MaxMembers,
            members.Single(m => m.Id == memberId), members);
    }
}
