namespace Ithaca.Application.Realtime;

/// <summary>
/// Pushes room events to connected members (docs/realtime.md). Implemented over SignalR in the Api next to RoomHub.
/// Services call it only AFTER SaveChangesAsync succeeded; payloads are small and carry ids, not whole entities.
/// Event names are the constants in RoomEvents (Api) = <c>contracts/realtime-events.json</c>.
/// </summary>
public interface IRoomEventPublisher
{
    Task PublishAsync<TPayload>(Guid roomId, string eventName, TPayload payload, CancellationToken ct)
        where TPayload : notnull;
}
