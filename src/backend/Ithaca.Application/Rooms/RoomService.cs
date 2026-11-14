using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Application.Decks;
using Ithaca.Application.Realtime;
using Ithaca.Domain.Rooms;
using Ithaca.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>
/// Group rooms and votes (api-contract.md §6, ADR-0012, ADR-0015). BE-10: create, preview, join, get, leave, close; the
/// mood step, start, deck and votes are BE-11's and the result BE-12's (contract stubs until then). Codes like
/// <c>POP-428</c> are case-insensitive. At most <see cref="Room.MaxMembers"/> present members, enforced by their seats.
/// The host is the longest-present member, so hosting passes on when the host leaves; the last one out closes the room.
/// Events are published after the commit; a failed publish never fails the request (<see cref="IRoomEventPublisher"/>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)] // BE-11/BE-12 stubs below
public sealed class RoomService(
    IAppDbContext db, ICurrentActor current, RoomAccess access, ISessionManager sessions, IRoomEventPublisher events, TimeProvider time)
{
    /// <summary>Tries at creating a room when a concurrent request drew the same code.</summary>
    private const int CreateAttempts = 3;

    /// <summary>
    /// Tries at joining. A failed try means a concurrent join took a seat (or this user joined from another tab), so after
    /// at most <see cref="Room.MaxMembers"/> of them the room is full or the caller is in: the loop always ends in an answer.
    /// </summary>
    private const int JoinAttempts = Room.MaxMembers + 1;

    /// <summary>The creator is the first member, so the host, of a new lobby. Nobody is listening yet: no event.</summary>
    public async Task<Result<RoomDto>> CreateAsync(CreateRoomRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (current.Actor is not { IsUser: true } actor)
        {
            return AuthErrors.Unauthenticated;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("name", "Enter a room name.");
        }

        for (var attempt = 1; ; attempt++)
        {
            var now = time.GetUtcNow();
            var room = Room.Create(RoomCode.New(), request.Name, request.Mode, request.StartsAt, now);
            var host = RoomMember.ForUser(room.Id, actor.Id, seat: 1, now);
            db.Rooms.Add(room);
            db.RoomMembers.Add(host);
            try
            {
                await db.SaveChangesAsync(ct);
                return await RoomDtoAsync(room.Id, host.Id, ct);
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e) && attempt < CreateAttempts)
            {
                // Another room took this code a moment ago: draw another.
                db.RoomMembers.Remove(host);
                db.Rooms.Remove(room);
            }
        }
    }

    public async Task<Result<RoomPreviewDto>> GetPreviewAsync(string code, CancellationToken ct)
    {
        if (await access.FindRoomIdAsync(code, ct) is not { } roomId)
        {
            return RoomErrors.NotFound;
        }

        var room = await db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => new { r.Code, r.Name, r.Mode, r.Status }).SingleAsync(ct);
        var members = await MembersAsync(roomId, room.Mode, ct);
        return new RoomPreviewDto(room.Code, room.Name, members.FirstOrDefault()?.DisplayName ?? "", room.Mode, room.Status, members.Count, Room.MaxMembers);
    }

    /// <summary>
    /// Idempotent: a present member gets the room again (no event). A signed-in user joins as themself (the name is
    /// ignored); anyone else (anonymous, or a guest of another room, A1) becomes a new <see cref="Guest"/> and gets a guest
    /// session for this room only. Joins are open in the lobby and while swiping.
    /// </summary>
    public async Task<Result<RoomDto>> JoinAsync(string code, JoinRoomRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await access.FindRoomIdAsync(code, ct) is not { } roomId)
        {
            return RoomErrors.NotFound;
        }

        var room = await db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => new { r.Code, r.Status }).SingleAsync(ct);
        var actor = current.Actor;
        Guest? guest = null;
        RoomMember member;
        for (var attempt = 1; ; attempt++)
        {
            var existing = actor is null ? null : await RoomAccess.OfActor(db.RoomMembers, roomId, actor).SingleOrDefaultAsync(ct);
            if (existing is { IsPresent: true })
            {
                return await RoomDtoAsync(roomId, existing.Id, ct);
            }

            if (!Room.AcceptsJoinsIn(room.Status))
            {
                return RoomErrors.Closed;
            }

            if (await FreeSeatAsync(roomId, ct) is not { } seat)
            {
                return RoomErrors.Full;
            }

            var now = time.GetUtcNow();
            if (actor is { IsUser: true })
            {
                member = existing ?? db.RoomMembers.Add(RoomMember.ForUser(roomId, actor.Id, seat, now)).Entity;
                member.Rejoin(seat, now);
            }
            else
            {
                var name = request.DisplayName?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    return Error.Validation("displayName", "Enter your name to join.");
                }

                guest ??= db.Guests.Add(Guest.Create(name, now)).Entity;
                member = db.RoomMembers.Add(RoomMember.ForGuest(roomId, guest.Id, seat, now)).Entity;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e) && attempt < JoinAttempts)
            {
                // Someone took this seat (or this user joined from another tab) at the same moment: look again.
                if (member == existing)
                {
                    await db.RoomMembers.Entry(member).ReloadAsync(ct);
                }
                else
                {
                    db.RoomMembers.Remove(member);
                }
            }
        }

        if (guest is not null)
        {
            await sessions.SignInGuestAsync(guest.Id, roomId, room.Code);
        }

        await events.PublishAsync(roomId, RoomEvents.MemberJoined, new RoomMemberEvent(roomId, member.Id), ct);
        return await RoomDtoAsync(roomId, member.Id, ct);
    }

    public async Task<Result<RoomDto>> GetAsync(string code, CancellationToken ct)
    {
        var membership = await access.MemberAsync(code, ct);
        return membership.IsSuccess ? await RoomDtoAsync(membership.Value.RoomId, membership.Value.MemberId, ct) : membership.Error;
    }

    public Task<Result<RoomMoodDto>> SetMoodAsync(string code, SetRoomMoodRequest request, CancellationToken ct) => ContractStub.Of<RoomMoodDto>();

    public Task<Result<RoomDto>> StartAsync(string code, CancellationToken ct) => ContractStub.Of<RoomDto>();

    /// <summary>The shared deck from <paramref name="fromPosition"/> on; each item's <c>swipe</c> is the caller's vote.</summary>
    public Task<Result<IReadOnlyList<DeckItemDto>>> GetDeckAsync(string code, int fromPosition, CancellationToken ct) =>
        ContractStub.Of<IReadOnlyList<DeckItemDto>>();

    public Task<Result> CastVoteAsync(string code, CastVoteRequest request, CancellationToken ct) => ContractStub.Command();

    public Task<Result<RoomProgressDto>> GetProgressAsync(string code, CancellationToken ct) => ContractStub.Of<RoomProgressDto>();

    /// <summary>Host ends swiping early: the result is the best compromise unless there is already a match.</summary>
    public Task<Result<RoomResultDto>> FinishAsync(string code, CancellationToken ct) => ContractStub.Of<RoomResultDto>();

    public Task<Result<RoomResultDto>> GetResultAsync(string code, CancellationToken ct) => ContractStub.Of<RoomResultDto>();

    /// <summary>
    /// The caller leaves (the host too: the next longest-present member hosts). The last one out closes the room. A guest's
    /// session ends with it (owner's rule, BE-10): coming back means joining as a new guest.
    /// </summary>
    public async Task<Result> LeaveAsync(string code, CancellationToken ct)
    {
        var membership = await access.MemberAsync(code, ct);
        if (!membership.IsSuccess)
        {
            return membership.Error;
        }

        var (roomId, memberId, actor) = membership.Value;
        var now = time.GetUtcNow();
        var member = await db.RoomMembers.SingleAsync(m => m.Id == memberId, ct);
        member.Leave(now);
        var closes = !await db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.LeftAt == null && m.Id != memberId, ct)
            && (await db.Rooms.SingleAsync(r => r.Id == roomId, ct)).Close(now);

        await db.SaveChangesAsync(ct);
        if (actor.IsGuest)
        {
            await sessions.SignOutAsync();
        }

        await events.PublishAsync(roomId, RoomEvents.MemberLeft, new RoomMemberEvent(roomId, memberId), ct);
        if (closes)
        {
            await events.PublishAsync(roomId, RoomEvents.RoomClosed, new RoomEvent(roomId), ct);
        }

        return Result.Success();
    }

    /// <summary>Host only. Ends the room for everyone; closing it again is still 204.</summary>
    public async Task<Result> CloseAsync(string code, CancellationToken ct)
    {
        var host = await access.HostAsync(code, ct);
        if (!host.IsSuccess)
        {
            return host.Error;
        }

        var roomId = host.Value.RoomId;
        if (!(await db.Rooms.SingleAsync(r => r.Id == roomId, ct)).Close(time.GetUtcNow()))
        {
            return Result.Success(); // already closed: nothing changed, nothing to announce
        }

        await db.SaveChangesAsync(ct);
        await events.PublishAsync(roomId, RoomEvents.RoomClosed, new RoomEvent(roomId), ct);
        return Result.Success();
    }

    /// <summary>The lowest seat no present member holds, or null when the room is full.</summary>
    private async Task<int?> FreeSeatAsync(Guid roomId, CancellationToken ct)
    {
        var taken = await db.RoomMembers.AsNoTracking().Where(m => m.RoomId == roomId && m.LeftAt == null).Select(m => m.Seat).ToListAsync(ct);
        return Enumerable.Range(1, Room.MaxMembers).Cast<int?>().FirstOrDefault(s => !taken.Contains(s!.Value));
    }

    /// <summary>Present members, host first, with their names (a user's from the account, a guest's from the join).</summary>
    private async Task<List<RoomMemberDto>> MembersAsync(Guid roomId, RoomMode mode, CancellationToken ct)
    {
        var users = db.Users;
        var guests = db.Guests.AsNoTracking();
        var rows = await RoomAccess.InHostOrder(db.RoomMembers.AsNoTracking().Where(m => m.RoomId == roomId && m.LeftAt == null))
            .Select(m => new
            {
                m.Id,
                IsUser = m.UserId != null,
                m.JoinedAt,
                Name = m.UserId != null
                    ? users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).FirstOrDefault()
                    : guests.Where(g => g.Id == m.GuestId).Select(g => g.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        // Readiness: in "just swipe" rooms everyone is ready; mood readiness arrives with the mood step (BE-11).
        return rows
            .Select((m, i) => new RoomMemberDto(
                m.Id, m.Name ?? "", m.IsUser ? PrincipalKind.User : PrincipalKind.Guest, IsHost: i == 0, IsReady: mode == RoomMode.JustSwipe, m.JoinedAt))
            .ToList();
    }

    private async Task<RoomDto> RoomDtoAsync(Guid roomId, Guid memberId, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking()
            .Where(r => r.Id == roomId)
            .Select(r => new { r.Id, r.Code, r.Name, r.Mode, r.Status, r.StartsAt, r.CreatedAt })
            .SingleAsync(ct);
        var members = await MembersAsync(roomId, room.Mode, ct);

        // The shared deck is dealt at start (BE-11); a lobby has none yet.
        return new RoomDto(
            room.Id, room.Code, room.Name, room.Mode, room.Status, room.StartsAt, room.CreatedAt, DeckSize: 0, Room.MaxMembers,
            members.Single(m => m.Id == memberId), members);
    }
}
