# Rooms

Group `Room`s and `RoomMember`s (users and `Guest`s), api-contract.md §6.

- `RoomService` — create, preview, join, get, leave, close (BE-10).
- `RoomResultService` — the host's finish and reading the result (BE-12).
- `RoomPickExplainer` — collaborator of `RoomResultService`: the AI's reason per pick, written on the first read and
  stored on the room (`Room.ResultReasons`); the votes decide the picks and order, the AI only explains (BE-13).
- `RoomReferee` — collaborator: decides a swiping room by `RoomResultRules` (Domain) after a vote, a leave or a finish,
  each after the caller's own commit; the room's `xmin` token makes the result unique (BE-12).
- `RoomDeckService` — the mood step, start, the shared deck, votes, progress and extension (BE-11). Deals through
  `DeckDealer` with `DeckFilter.ForRoom` (members' moods + hard nos).
- `RoomReader` — collaborator: `RoomDto`, members with readiness, the room's deck items.
- `RoomMembershipService` — the hub's `JoinRoom` / `LeaveRoom` (docs/realtime.md).
- `RoomAccess` — collaborator: the one member / host rule (host = present longest). `GuestUpgrade` — collaborator of
  `AuthService`: a guest who registers or signs in keeps their membership.
- Publishes through `IRoomEventPublisher` after commit, with the names in `Realtime/RoomEvents` (ADR-0025).
