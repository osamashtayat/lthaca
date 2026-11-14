# Rooms

Group `Room`s and `RoomMember`s (users and `Guest`s), api-contract.md §6.

- `RoomService` — the room endpoints. BE-10: create, preview, join, get, leave, close. Mood step, start, deck, votes
  (BE-11) and the result (BE-12) are contract stubs until then.
- `RoomMembershipService` — the hub's `JoinRoom` / `LeaveRoom` (docs/realtime.md).
- `RoomAccess` — collaborator: the one member / host rule (host = present longest). `GuestUpgrade` — collaborator of
  `AuthService`: a guest who registers or signs in keeps their membership.
- Publishes through `IRoomEventPublisher` after commit, with the names in `Realtime/RoomEvents` (ADR-0025).
