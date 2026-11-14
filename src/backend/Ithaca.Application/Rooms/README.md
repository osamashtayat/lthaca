# Rooms

Group `Room`s and `RoomMember`s (users and `Guest`s), api-contract.md §6.

- `RoomService` — create, preview, join, get, leave, close (BE-10). Finish and result (BE-12) are contract stubs.
- `RoomDeckService` — the mood step, start, the shared deck, votes, progress and extension (BE-11). Deals through
  `DeckDealer` with `DeckFilter.ForRoom` (members' moods + hard nos).
- `RoomReader` — collaborator: `RoomDto`, members with readiness, the room's deck items.
- `RoomMembershipService` — the hub's `JoinRoom` / `LeaveRoom` (docs/realtime.md).
- `RoomAccess` — collaborator: the one member / host rule (host = present longest). `GuestUpgrade` — collaborator of
  `AuthService`: a guest who registers or signs in keeps their membership.
- Publishes through `IRoomEventPublisher` after commit, with the names in `Realtime/RoomEvents` (ADR-0025).
