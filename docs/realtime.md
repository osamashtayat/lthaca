# Real-time: rooms and RoomHub

One hub, `RoomHub` at **`/hubs/rooms`** (Ithaca.Api/Hubs), for group rooms. State lives in the database; the hub only
pushes "something changed" hints and receives member actions that it forwards to services.

## Contract
`contracts/realtime-events.json` is the single list of server → client events. The backend mirrors it in
`RoomEvents.cs` (`RealtimeContractTests` fails on drift); the frontend imports the JSON (`src/shared/realtime/events.ts`).

| Event | Payload | Client reaction |
|---|---|---|
| `memberJoined` | `{ roomId, memberId }` | refetch room members |
| `memberLeft` | `{ roomId, memberId }` | refetch room members |
| `voteCast` | `{ roomId, deckItemId }` | refetch progress (no vote values in the event) |
| `deckExtended` | `{ roomId, fromPosition }` | refetch the deck from that position |
| `roomMatched` | `{ roomId, tmdbId }` | go to the result screen |
| `roomClosed` | `{ roomId }` | leave the room screen |

Payloads carry ids, never whole objects or other members' votes; the client fetches details through the API so
authorization stays in one place. Group name per room: `RoomEvents.GroupFor(roomId)` → `room:<guid>`.

## Server rules
- `RoomHub` is anonymous (guests have no account). The rooms feature decides how a connection proves membership
  (e.g. a signed room/guest token passed to a `JoinRoom` hub method) — open decision, see the report.
- Hub methods are thin: call one Application service, return. Group membership changes happen only after the service
  confirmed the member.
- Services publish through `IRoomEventPublisher.PublishAsync(roomId, RoomEvents.X, payload, ct)` **after** `SaveChangesAsync`.
  A failed publish must not fail the request (clients refetch on reconnect).
- Skeleton status: the hub is mapped and connectable; it has no methods yet.

## Client rules
- `useRoomHub({ enabled, handlers })` is the only hub access. It opens one connection per mounted screen, auto-reconnects
  (0, 2, 10, 30 s) and reports `connecting | connected | reconnecting | disconnected`.
- Handlers invalidate TanStack Query keys; on reconnect, invalidate the room's queries (events may have been missed).
- Votes and swipes are sent as HTTP mutations (retry 0), not hub calls, unless a later ADR decides otherwise.

## Transport
Same origin: Vite proxies `/hubs` with `ws: true` in dev; nginx forwards WebSocket upgrades in Docker.
