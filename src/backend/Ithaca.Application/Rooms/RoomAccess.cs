using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>The caller's present membership of a room, as <see cref="RoomAccess.MemberAsync"/> confirmed it.</summary>
public sealed record RoomMembership(Guid RoomId, Guid MemberId, Actor Actor);

/// <summary>
/// The one "who may touch this room" rule (api-contract.md §1 Access), shared by <see cref="RoomService"/> and
/// <see cref="RoomMembershipService"/>; a collaborator, not a use-case service. A user is a member through their own
/// membership; a guest only in the one room their cookie is scoped to (ADR-0012). The host is the present member who
/// joined earliest (<see cref="InHostOrder"/>).
/// </summary>
public sealed class RoomAccess(IAppDbContext db, ICurrentActor current)
{
    /// <summary>The room's id, or null when no room has that code (a malformed code included).</summary>
    public Task<Guid?> FindRoomIdAsync(string code, CancellationToken ct)
    {
        var normalized = RoomCode.Normalize(code);
        return db.Rooms.AsNoTracking().Where(r => r.Code == normalized).Select(r => (Guid?)r.Id).SingleOrDefaultAsync(ct);
    }

    /// <summary><c>room.not_found</c>, then <c>auth.unauthenticated</c>, then <c>room.not_member</c>.</summary>
    public async Task<Result<RoomMembership>> MemberAsync(string code, CancellationToken ct)
    {
        if (await FindRoomIdAsync(code, ct) is not { } roomId)
        {
            return RoomErrors.NotFound;
        }

        if (current.Actor is not { } actor)
        {
            return AuthErrors.Unauthenticated;
        }

        var memberId = await OfActor(db.RoomMembers.AsNoTracking(), roomId, actor)
            .Where(m => m.LeftAt == null)
            .Select(m => (Guid?)m.Id)
            .SingleOrDefaultAsync(ct);
        return memberId is { } id ? new RoomMembership(roomId, id, actor) : RoomErrors.NotMember;
    }

    /// <summary>As <see cref="MemberAsync"/>, and the member must be the host (<c>room.not_host</c>).</summary>
    public async Task<Result<RoomMembership>> HostAsync(string code, CancellationToken ct)
    {
        var membership = await MemberAsync(code, ct);
        if (!membership.IsSuccess)
        {
            return membership;
        }

        var hostId = await InHostOrder(db.RoomMembers.AsNoTracking().Where(m => m.RoomId == membership.Value.RoomId && m.LeftAt == null))
            .Select(m => m.Id)
            .FirstAsync(ct);
        return hostId == membership.Value.MemberId ? membership : RoomErrors.NotHost;
    }

    /// <summary>The actor's membership row in the room, present or not. A guest has none outside their own room.</summary>
    public static IQueryable<RoomMember> OfActor(IQueryable<RoomMember> members, Guid roomId, Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.IsUser)
        {
            return members.Where(m => m.RoomId == roomId && m.UserId == actor.Id);
        }

        return actor.RoomId == roomId
            ? members.Where(m => m.RoomId == roomId && m.GuestId == actor.Id)
            : members.Where(_ => false);
    }

    /// <summary>Host first: the longest-present member hosts (owner's rule, BE-10).</summary>
    public static IOrderedQueryable<RoomMember> InHostOrder(IQueryable<RoomMember> members) =>
        members.OrderBy(m => m.JoinedAt).ThenBy(m => m.Id);
}
