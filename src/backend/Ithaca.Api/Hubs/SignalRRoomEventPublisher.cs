using Ithaca.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Ithaca.Api.Hubs;

/// <summary>
/// <see cref="IRoomEventPublisher"/> over SignalR. Lives in the Api because it needs <c>IHubContext&lt;RoomHub&gt;</c>
/// (ADR-0007). Application services call it after their commit; it never decides anything.
/// </summary>
internal sealed class SignalRRoomEventPublisher(IHubContext<RoomHub> hub) : IRoomEventPublisher
{
    public Task PublishAsync<TPayload>(Guid roomId, string eventName, TPayload payload, CancellationToken ct)
        where TPayload : notnull =>
        hub.Clients.Group(RoomEvents.GroupFor(roomId)).SendAsync(eventName, payload, ct);
}
