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
- Auth (ADR-0012): users and guests both carry a cookie principal (a guest's is scoped to one room). A `JoinRoom(code)` hub
  method asks a service to confirm the caller is a `RoomMember` before adding the connection to the room's group.
- Hub methods are thin: call one Application service, return. Group membership changes happen only after the service
  confirmed the member.
- Services publish through `IRoomEventPublisher.PublishAsync(roomId, RoomEvents.X, payload, ct)` **after** `SaveChangesAsync`.
  A failed publish must not fail the request (clients refetch on reconnect).
- Skeleton status: the hub is mapped and connectable; it has no methods yet.

## Client rules
- `useRoomHub({ enabled, handlers })` is the only hub access. It opens one connection per mounted screen, auto-reconnects
  (0, 2, 10, 30 s) and reports `connecting | connected | reconnecting | disconnected`.
- `useRoomHub({ room: code, onSynced })` also invokes `JoinRoom(code)` after the first connect and after every reconnect (the
  server drops group membership with the connection), then calls `onSynced`. Rooms use it through `useRoomLive(code, enabled)`
  (features/rooms), which refetches the room on `memberJoined`, `memberLeft`, `roomMatched`, `roomClosed` and on every sync.
- There is no event for "host started" or "member ready" yet, so the lobby also polls the room every 4 s while `status` is `lobby`.
- Handlers invalidate TanStack Query keys; on reconnect, invalidate the room's queries (events may have been missed).
- Votes and swipes are HTTP mutations (retry 0) with a client-generated id, never hub calls (ADR-0015).
- Scaling out needs a SignalR backplane first (ADR-0016).

## Transport
Same origin: Vite proxies `/hubs` with `ws: true` in dev; nginx forwards WebSocket upgrades in Docker.
