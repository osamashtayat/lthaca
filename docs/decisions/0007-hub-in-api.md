# ADR-0007: RoomHub and its publisher live in the Api (deviation from FlowOps)

- **Status:** Accepted; amended by [ADR-0025](0025-room-events-in-application.md) (`RoomEvents` moved to Application)
- **Date:** 2026-11-12

## Context
FlowOps keeps `NotificationsHub` and its SignalR publisher in Infrastructure because the hub needs Identity's `SignInManager`. The brief puts SignalR hubs in `Ithaca.Api`.

## Decision
- `RoomHub`, `RoomEvents` and `SignalRRoomEventPublisher` are in `Ithaca.Api/Hubs/`. The publisher needs `IHubContext<RoomHub>`, so it lives next to the hub.
- Application depends only on `IRoomEventPublisher`. Event names come from `contracts/realtime-events.json` (one source, shared with the frontend; drift test in Ithaca.Tests).
- Hubs follow the controller rule: no logic, one service call per method.

## Consequences
+ Hubs sit with the other transport adapters (controllers); Infrastructure stays free of ASP.NET Core endpoints.
- If hub auth later needs Identity types, they must be reached through an Application interface, not referenced directly.

## Alternatives rejected
Hub in Infrastructure (FlowOps): conflicts with the brief.
