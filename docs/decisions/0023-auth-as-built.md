# ADR-0023: Auth as built: where it deviates from FlowOps

- **Status:** Accepted (owner chose the session lifetime and the user-only enforcement, 2026-11-13)
- **Date:** 2026-11-13

## Context
ADR-0012 fixed the design (Identity cookies for users, room-scoped guest cookies in the same scheme, antiforgery).
BE-02 builds it by copying FlowOps' ADR-0003 setup (Identity in Infrastructure, `ICurrentUser`-style port,
`ValidateAntiforgeryFilter` + `[IssueXsrfToken]`). Ithaca is a consumer phone app with public sign-up and anonymous
guests, so some FlowOps choices don't fit. This records each deviation.

## Decision
- **Public registration** (`POST /api/auth/register`), email + password. FlowOps has admin-created accounts only.
- **Passwords: length only** (8–128, NIST SP 800-63B); no digit/case/symbol rules. Lockout as FlowOps: 5 failures,
  15 minutes; unknown email, wrong password and locked account all answer `auth.invalid_credentials`.
- **Session: persistent cookie, 30 days, sliding** (owner's choice). FlowOps: 8 hours. Phone users rarely sign in again.
- **SameSite=Lax, no `__Host-` prefix, Secure only in Production** (FlowOps: Strict, `__Host-`, always Secure). Lax so a
  shared room link opened from a chat app arrives with the session; Development is plain HTTP (ADR-0009), where Secure
  and `__Host-` cookies are never sent back. Names: `ithaca_session`, `ithaca_af` (antiforgery), `XSRF-TOKEN` (readable).
- **No antiforgery exemption, not even sign-in** (FlowOps exempts login). The SPA gets its first token from
  `GET /api/auth/me`, which issues it even when it answers 401. Failure is **400 `auth.antiforgery_failed`** (FlowOps:
  403 `auth.forbidden`), so the client can tell "refresh the token" from "not allowed".
- **Guests in Identity's cookie scheme**: a principal with `ithaca:kind=guest`, the guest id, room id and room code.
  Identity's periodic security-stamp check is skipped for guests (they have no account); users keep it.
- **One `ICurrentActor`** (user or guest) instead of FlowOps' user-only `ICurrentUser`.
- **No roles**: `IdentityUserContext` (tables `users`, `user_claims`, `user_logins`, `user_tokens`), no role tables.
  `UserName` is the user id, so any valid email can sign up. Identity's v1 schema (no passkey table).
- **Access is enforced declaratively for user-only endpoints** (`[Authorize(Policy = AuthPolicies.User)]`, owner's
  choice): anonymous callers and guests get 401 `auth.unauthenticated`. Member and host checks need room data and stay
  in the room services (BE-10…BE-12).

## Consequences
+ Sign-in stays rare on phones; shared links work from other apps; one auth mechanism for users, guests and SignalR.
+ No unprotected unsafe endpoint, and a distinct code for a stale XSRF token.
- A stolen session cookie lives up to 30 days (mitigated: HttpOnly, sign-out clears it, a security-stamp change ends it).
- The SPA must call `/api/auth/me` before its first unsafe request, and pick up the new token after every sign-in,
  sign-out and room join.

## Alternatives rejected
FlowOps' 8-hour session (constant re-sign-in on phones); exempting sign-in from antiforgery (login CSRF); Strict
cookies (shared links would arrive signed out); a separate guest scheme (two mechanisms, ADR-0012).
