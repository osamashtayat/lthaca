# ADR-0015: Votes are HTTP requests; the hub only broadcasts

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Group votes could be sent as SignalR hub calls or as HTTP requests. Phones drop WebSockets often; votes must never be double-counted; every write should go through the same validation, auth and error pipeline.

## Decision
- A vote is `POST /api/rooms/{code}/votes` with a client-generated vote id. A unique constraint on (`DeckItem`, `RoomMember`) plus the id makes retries idempotent.
- After the commit, the service publishes `voteCast` through `IRoomEventPublisher`; clients refetch progress. Events never carry vote values.
- Hub methods stay limited to connection concerns (`JoinRoom`, `LeaveRoom`).

## Consequences
+ Reuses Result → ProblemDetails, auth, tests; works while the socket reconnects.
- A few tens of ms more latency per swipe than a hub call (not noticeable).

## Alternatives rejected
Hub-invoked votes (second validation/error path, lost votes on reconnect).
