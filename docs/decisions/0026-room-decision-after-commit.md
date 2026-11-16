# ADR-0026: A room is decided after the commit that triggers it

- **Status:** Accepted (BE-12)
- **Date:** 2026-11-15
- **Amends:** docs/backend-conventions.md → Persistence ("`SaveChangesAsync` once")

## Context
BE-12 must turn the room's result into `result` with "exactly one match under concurrency". The deciding moment is a
vote (or a member leaving, or the host's finish). Two members casting the last "right" votes at the same moment each
run in their own transaction and, at read committed, can't see the other's uncommitted vote: if each request decided
inside its own unit of work, neither would see unanimity and the match would be missed. Vote inserts don't touch the
room row, so the room's `xmin` token alone can't serialize them.

## Decision
`RoomReferee` (Application/Rooms) decides **after** the triggering request's own `SaveChangesAsync`, with a second
`SaveChangesAsync` of its own that changes only the room (`Room.Decide`). Whichever of two racing votes commits last
reads both, so no match is missed. Two deciders racing are settled by the room's `xmin` token: the loser reloads, finds
the room decided (or closed) and does nothing, so there is one result and one `roomMatched`. The decision stores the
ids of the votes it counted (`Room.ResultVotes`), so a vote committed just after can't change the result. A retried
vote that finds its `voteId` already stored runs the referee again, in case the first try failed before deciding.

This is the second allowed exception to "one `SaveChangesAsync` per use case", next to resumable batch jobs.

## Consequences
+ A match can't be missed and can't be decided twice, without locks or serializable transactions.
- A vote's request does two commits; a failure in the second leaves the vote stored and the room undecided until the
  retry, the next vote, a leave or the host's finish.
- Every vote reads the deck's tallies (≤ 30 cards).

## Alternatives rejected
Deciding inside the vote's transaction (misses concurrent last votes); touching the room row on every vote so votes
conflict and retry (serializes all swiping in a room); `SELECT … FOR UPDATE` or serializable isolation (needs database
access outside `IAppDbContext`).
