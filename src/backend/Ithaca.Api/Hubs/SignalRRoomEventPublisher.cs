using Ithaca.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Ithaca.Api.Hubs;

/// <summary>
/// <see cref="IRoomEventPublisher"/> over SignalR. Lives in the Api because it needs <c>IHubContext&lt;RoomHub&gt;</c>
/// (ADR-0007). Application services call it after their commit; it never decides anything. The one place a failed
/// publish is caught: the change is already committed and clients refetch on reconnect, so it is logged, never thrown.
/// </summary>
internal sealed partial class SignalRRoomEventPublisher(IHubContext<RoomHub> hub, ILogger<SignalRRoomEventPublisher> logger)
    : IRoomEventPublisher
{
    public async Task PublishAsync<TPayload>(Guid roomId, string eventName, TPayload payload, CancellationToken ct)
        where TPayload : notnull
    {
        try
        {
            await hub.Clients.Group(RoomEvents.GroupFor(roomId)).SendAsync(eventName, payload, ct);
        }
#pragma warning disable CA1031 // Any failure (transport, serialization, cancellation) must not fail the committed request.
        catch (Exception e)
#pragma warning restore CA1031
        {
            LogPublishFailed(logger, e, eventName, roomId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing {EventName} to room {RoomId} failed")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string eventName, Guid roomId);
}
