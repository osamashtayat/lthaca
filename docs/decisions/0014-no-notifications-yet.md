# ADR-0014: No notifications feature for now

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
The design canvas has a bell and an "Updates" screen, but notifications weren't in the brief and the owner confirmed they aren't needed yet.

## Decision
- No bell in the header, no `/updates` route, no "Updates" menu item, no `Notification` entity.
- When needed, scope it first to room events (a friend started a movie night, a room matched) built on what rooms already produce; add a glossary term and a new ADR then.

## Consequences
+ Less UI and backend surface now.
- The header differs from the canvas until notifications return.

## Alternatives rejected
Building a generic notification system up front.
