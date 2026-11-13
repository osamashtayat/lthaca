using Ithaca.Application.Realtime;
using Ithaca.Application.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ithaca.Api.Hubs;

/// <summary>
/// The one real-time hub (<c>/hubs/rooms</c>). Like a controller it holds NO logic: each method calls one Application
/// service and returns. Anonymous because guests join rooms without an account; <see cref="RoomMembershipService"/>
/// decides whether the caller (users and guests alike, from the session cookie) may join a room's group (ADR-0012).
/// The connection's principal is the one it connected with: a guest who joined over HTTP afterwards reconnects.
/// Votes are HTTP (ADR-0015); clients refetch through the API when an event arrives.
/// </summary>
[AllowAnonymous]
public sealed class RoomHub(RoomMembershipService membership) : Hub
{
    /// <summary>Adds this connection to the room's group, members only. Fails with a <see cref="HubException"/> carrying the error code.</summary>
    public async Task JoinRoom(string code) =>
        await Groups.AddToGroupAsync(
            Context.ConnectionId, RoomEvents.GroupFor((await membership.AuthorizeAsync(code, Context.ConnectionAborted)).OrThrowHubException()));

    /// <summary>Stops this connection's events from the room. Not leaving the room (that is <c>POST …/leave</c>).</summary>
    public async Task LeaveRoom(string code) =>
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId, RoomEvents.GroupFor((await membership.FindRoomAsync(code, Context.ConnectionAborted)).OrThrowHubException()));
}
