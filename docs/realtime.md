# Real-time: rooms and RoomHub

One hub, `RoomHub` at **`/hubs/rooms`** (Ithaca.Api/Hubs), for group rooms. State lives in the database; the hub only
pushes "something changed" hints and receives member actions that it forwards to services.

## Contract
`contracts/realtime-events.json` is the single list of server → client events. The backend mirrors it in
`Ithaca.Application/Realtime/RoomEvents.cs` (ADR-0025; `RealtimeContractTests` fails on drift); the frontend imports the JSON (`src/shared/realtime/events.ts`).

| Event | Payload | Client reaction |
|---|---|---|
| `memberJoined` | `{ roomId, memberId }` | refetch room members |
| `memberLeft` | `{ roomId, memberId }` | refetch room members |
| `memberReady` | `{ roomId, memberId }` | refetch the room (readiness, "2 OF 3 READY") |
| `roomStarted` | `{ roomId }` | refetch the room; `swiping` → go to the room deck |
| `voteCast` | `{ roomId, deckItemId }` | refetch progress (no vote values in the event) |
| `deckExtended` | `{ roomId, fromPosition }` | refetch the deck from that position |
| `roomMatched` | `{ roomId, tmdbId }` | go to the result screen |
| `roomClosed` | `{ roomId }` | leave the room screen |

Payloads carry ids, never whole objects or other members' votes; the client fetches details through the API so
authorization stays in one place. Group name per room: `RoomEvents.GroupFor(roomId)` → `room:<guid>`.

## Server rules
- Auth (ADR-0012): users and guests both carry a cookie principal (a guest's is scoped to one room). The `JoinRoom(code)` hub
  method asks `RoomMembershipService` to confirm the caller is a present `RoomMember` before adding the connection to the
  room's group; a refusal is a `HubException` whose message is the error code. `LeaveRoom(code)` removes the connection
  from the group (it doesn't leave the room).
- In a hub method the caller is the connection's principal: `HubCallerPrincipalFilter` hands it to `ICurrentActor`, so
  services read users and guests the same way as over HTTP. A principal is fixed when the connection starts, so a client
  that joins a room (or signs in) over HTTP reconnects before calling `JoinRoom`.
- Known gap: connections of a member who left, or of a closed room, stay in the group until they disconnect or call
  `LeaveRoom`; events carry only ids, and every refetch is authorized (issue #40).
- Publishers: `memberJoined`/`memberLeft`/`roomClosed` BE-10; `memberReady` (after `PUT …/mood`, mood rooms only),
  `roomStarted` (after `POST …/start`), `voteCast`, `deckExtended` BE-11; `roomMatched` BE-12.
- Hub methods are thin: call one Application service, return. Group membership changes happen only after the service
  confirmed the member.
- Services publish through `IRoomEventPublisher.PublishAsync(roomId, RoomEvents.X, payload, ct)` **after** `SaveChangesAsync`,
  with the payload records `RoomMemberEvent` / `RoomEvent` (Application/Realtime). A failed publish must not fail the
  request (clients refetch on reconnect): `SignalRRoomEventPublisher` is the one place that catches and logs it.
- Built (BE-10): `JoinRoom`, `LeaveRoom`; `memberJoined` (a new or returning member, not a repeated join), `memberLeft`,
  `roomClosed` (host close, or the last member leaving).

## Client rules
- `useRoomHub({ enabled, handlers })` is the only hub access. It opens one connection per mounted screen, auto-reconnects
  (0, 2, 10, 30 s) and reports `connecting | connected | reconnecting | disconnected`.
- `useRoomHub({ room: code, onSynced })` also invokes `JoinRoom(code)` after the first connect and after every reconnect (the
  server drops group membership with the connection), then calls `onSynced`. Rooms use it through `useRoomLive(code, enabled)`
  (features/rooms), which refetches the room on `memberJoined`, `memberLeft`, `memberReady`, `roomStarted`, `roomMatched`, `roomClosed` and on every sync.
- Handlers invalidate TanStack Query keys; on reconnect, invalidate the room's queries (events may have been missed).
- Votes and swipes are HTTP mutations (retry 0) with a client-generated id, never hub calls (ADR-0015).
- Scaling out needs a SignalR backplane first (ADR-0016).

## Transport
Same origin: Vite proxies `/hubs` with `ws: true` in dev; nginx forwards WebSocket upgrades in Docker.
