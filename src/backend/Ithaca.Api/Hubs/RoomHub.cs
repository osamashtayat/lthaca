using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ithaca.Api.Hubs;

/// <summary>
/// The one real-time hub (<c>/hubs/rooms</c>). Like a controller it holds NO logic: each hub method validates nothing
/// itself, calls one Application service and returns. Anonymous because guests join rooms without an account; the room
/// feature decides how a connection proves membership (docs/realtime.md).
/// SKELETON: connection only. Client → server methods (JoinRoom, CastVote…) are added by the rooms feature.
/// </summary>
[AllowAnonymous]
public sealed class RoomHub : Hub;
