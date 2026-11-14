# ADR-0025: Room event names live in Application

- **Status:** Accepted (BE-10)
- **Date:** 2026-11-13
- **Amends:** ADR-0007 (where `RoomEvents` lives)

## Context
ADR-0007 put `RoomHub`, `RoomEvents` and `SignalRRoomEventPublisher` in `Ithaca.Api/Hubs/`. docs/realtime.md and
AGENTS.md also say services publish `IRoomEventPublisher.PublishAsync(roomId, RoomEvents.X, …)` after their commit. Both
can't hold: services are in `Ithaca.Application`, which must not reference the Api (ADR-0001, `LayerDependencyTests`).
BE-10 is the first task that publishes, so it hit this.

## Decision
`RoomEvents` (event names, `HubPath`, `GroupFor`) and the payload records (`RoomMemberEvent`, `RoomEvent`) move to
`Ithaca.Application/Realtime/`, next to `IRoomEventPublisher`. The hub and the SignalR publisher stay in the Api
(ADR-0007 otherwise unchanged). `contracts/realtime-events.json` remains the source of truth; `RealtimeContractTests`
still compares it with `RoomEvents`.

## Consequences
+ Services name the event they publish, with no string duplicated across layers.
- The Api reads its hub path and group names from Application (allowed: Api → Application).

## Alternatives rejected
One publisher method per event on `IRoomEventPublisher` (an interface change per event, and the names would still be
needed by the Api); a second copy of the names in Application (two lists to keep equal).
