using Ithaca.Application.Common;

namespace Ithaca.Application.Rooms;

/// <summary>
/// Hub membership (docs/realtime.md, ADR-0012): a connection joins a room's group only after this confirms the caller is
/// a present member. The client can never self-assert membership.
/// </summary>
public sealed class RoomMembershipService(RoomAccess access)
{
    /// <summary>The room id whose group the caller may join; <c>room.not_found</c> / <c>auth.unauthenticated</c> / <c>room.not_member</c>.</summary>
    public async Task<Result<Guid>> AuthorizeAsync(string code, CancellationToken ct)
    {
        var membership = await access.MemberAsync(code, ct);
        return membership.IsSuccess ? membership.Value.RoomId : membership.Error;
    }

    /// <summary>The room id to stop listening to. Leaving a group needs no membership.</summary>
    public async Task<Result<Guid>> FindRoomAsync(string code, CancellationToken ct) =>
        await access.FindRoomIdAsync(code, ct) is { } roomId ? roomId : RoomErrors.NotFound;
}
