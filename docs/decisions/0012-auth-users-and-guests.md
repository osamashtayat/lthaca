# ADR-0012: Cookie auth for users; guests as room-scoped cookie identities

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Ithaca has registered `User`s and `Guest`s who join a room from a link without an account. Phones are the main client, SignalR runs on the same origin, and FlowOps already solved cookie auth with ASP.NET Core Identity (its ADR-0003).

## Decision
- **Users:** ASP.NET Core Identity with cookie authentication (HttpOnly, SameSite=Lax; `Secure` in Production). Start with email + password; add Google/Apple sign-in later. No tokens in browser storage.
- **Guests:** `POST /api/rooms/{code}/join` with a display name creates a `Guest` + `RoomMember` and issues a cookie in the same scheme, carrying the guest id and the room id, signed by ASP.NET Data Protection. A guest principal is valid only for that room.
- **Hub membership:** clients call a `JoinRoom(code)` hub method; it asks a service to confirm the caller is a `RoomMember` before adding the connection to the room's group. The client can never self-assert membership.
- **Upgrade:** a guest who signs up keeps their votes (moved to the new `User`).
- **Local HTTPS stays off** (ADR-0009): Development uses non-Secure Lax cookies over HTTP; revisit only if phone testing on the LAN shows cookie problems.

## Consequences
+ One auth mechanism for HTTP and SignalR; nothing secret readable by JavaScript.
+ Guests stay low-friction (no password), but scoped.
- Antiforgery is needed for unsafe requests (FlowOps' XSRF pattern) once auth lands.
- Not built yet: this ADR fixes the design for the auth and rooms features.

## Alternatives rejected
JWT in local storage (XSS-exposed, harder SignalR auth); anonymous hub with client-sent member ids (spoofable); separate guest token scheme (two mechanisms).
