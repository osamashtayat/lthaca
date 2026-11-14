using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Application.Realtime;
using Ithaca.Domain.Rooms;
using Ithaca.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>
/// Group rooms (api-contract.md §6, ADR-0012). BE-10: create, preview, join, get, leave, close. The mood step, start, deck,
/// votes and progress are <see cref="RoomDeckService"/>'s (BE-11); the result is BE-12's (contract stubs until then). Codes like
/// <c>POP-428</c> are case-insensitive. At most <see cref="Room.MaxMembers"/> present members, enforced by their seats.
/// The host is the longest-present member, so hosting passes on when the host leaves; the last one out closes the room.
/// Events are published after the commit; a failed publish never fails the request (<see cref="IRoomEventPublisher"/>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)] // BE-12 stubs below
public sealed class RoomService(
    IAppDbContext db, ICurrentActor current, RoomAccess access, RoomReader reader, ISessionManager sessions, IRoomEventPublisher events, TimeProvider time)
{
    /// <summary>Tries at creating a room when a concurrent request drew the same code.</summary>
    private const int CreateAttempts = 3;

    /// <summary>
    /// Tries at joining. A failed try means a concurrent join took a seat (or this user joined from another tab), so after
    /// at most <see cref="Room.MaxMembers"/> of them the room is full or the caller is in: the loop always ends in an answer.
    /// </summary>
    private const int JoinAttempts = Room.MaxMembers + 1;

    /// <summary>Tries at closing a room when a concurrent start changed it first (rooms carry a concurrency token).</summary>
    private const int WriteAttempts = 3;

    /// <summary>The creator is the first member, so the host, of a new lobby. Nobody is listening yet: no event.</summary>
    public async Task<Result<RoomDto>> CreateAsync(CreateRoomRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (current.Actor is not { IsUser: true } actor)
        {
            return AuthErrors.Unauthenticated;
        }

        if (request.Mode == RoomMode.Taste)
        {
            return ContractStub.NotImplemented; // group taste: BE-14 (api-contract.md §12)
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
                return await reader.RoomDtoAsync(room.Id, host.Id, ct);
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
        var members = await reader.MembersAsync(roomId, room.Mode, ct);
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
                return await reader.RoomDtoAsync(roomId, existing.Id, ct);
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
        return await reader.RoomDtoAsync(roomId, member.Id, ct);
    }

    public async Task<Result<RoomDto>> GetAsync(string code, CancellationToken ct)
    {
        var membership = await access.MemberAsync(code, ct);
        return membership.IsSuccess ? await reader.RoomDtoAsync(membership.Value.RoomId, membership.Value.MemberId, ct) : membership.Error;
    }

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
        bool closes;
        for (var attempt = 1; ; attempt++)
        {
            closes = !await db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.LeftAt == null && m.Id != memberId, ct)
                && (await db.Rooms.SingleAsync(r => r.Id == roomId, ct)).Close(now);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e) && closes && attempt < WriteAttempts)
            {
                // The host started the room at the same moment (rooms carry a concurrency token): read it again, close again.
                await db.Rooms.Entry(await db.Rooms.SingleAsync(r => r.Id == roomId, ct)).ReloadAsync(ct);
            }
        }
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
        for (var attempt = 1; ; attempt++)
        {
            var room = await db.Rooms.SingleAsync(r => r.Id == roomId, ct);
            if (!room.Close(time.GetUtcNow()))
            {
                return Result.Success(); // already closed: nothing changed, nothing to announce
            }

            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e) && attempt < WriteAttempts)
            {
                // A start (or a leave) changed the room at the same moment: read it again and decide again.
                await db.Rooms.Entry(room).ReloadAsync(ct);
            }
        }
        await events.PublishAsync(roomId, RoomEvents.RoomClosed, new RoomEvent(roomId), ct);
        return Result.Success();
    }

    /// <summary>The lowest seat no present member holds, or null when the room is full.</summary>
    private async Task<int?> FreeSeatAsync(Guid roomId, CancellationToken ct)
    {
        var taken = await db.RoomMembers.AsNoTracking().Where(m => m.RoomId == roomId && m.LeftAt == null).Select(m => m.Seat).ToListAsync(ct);
        return Enumerable.Range(1, Room.MaxMembers).Cast<int?>().FirstOrDefault(s => !taken.Contains(s!.Value));
    }
}
