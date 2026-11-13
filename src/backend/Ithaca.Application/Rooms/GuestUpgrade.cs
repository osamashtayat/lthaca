using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>
/// Guest upgrade (ADR-0012, api-contract.md §4): a guest who registers or signs in keeps their place in the room; their
/// membership (and so their votes, BE-11) moves to the user. A collaborator of <c>AuthService</c>; the caller saves.
/// If the user is already present in that room, their own membership wins and the guest's ends. A user's earlier,
/// already-left membership of that room gives way to the guest's (it is removed), so the room keeps one row per user.
/// </summary>
public sealed class GuestUpgrade(IAppDbContext db, TimeProvider time)
{
    /// <summary>Stages the move; false when there was nothing to move (not a guest, or no longer in the room).</summary>
    public async Task<bool> StageAsync(Actor? previous, Guid userId, CancellationToken ct)
    {
        if (previous is not { IsGuest: true, RoomId: { } roomId })
        {
            return false;
        }

        var guestMember = await RoomAccess.OfActor(db.RoomMembers, roomId, previous).SingleOrDefaultAsync(m => m.LeftAt == null, ct);
        if (guestMember is null)
        {
            return false;
        }

        var userMember = await db.RoomMembers.SingleOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        if (userMember is { IsPresent: true })
        {
            guestMember.Leave(time.GetUtcNow());
            return true;
        }

        if (userMember is not null)
        {
            db.RoomMembers.Remove(userMember);
        }

        guestMember.TransferToUser(userId);
        return true;
    }
}
