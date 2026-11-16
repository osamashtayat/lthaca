# ADR-0027: The security stamp is checked on every request

- **Status:** Accepted (BE-16)
- **Date:** 2026-11-16
- **Amends:** ADR-0023 (auth as built): Identity's default stamp check every 30 minutes

## Context
The profile contract (api-contract.md §14) needs two things to take effect at once:
- A password change ends every other session of that user ("a stolen session alone can't lock the owner out").
- A deleted account's sessions stop working, since every row they would write points at a user who is gone.

A session cookie stays valid until ASP.NET Core Identity's `SecurityStampValidator` compares the stamp in the cookie with
the user's current one. By default it does this every 30 minutes. With that default, an old session would keep working
for up to 30 minutes after a password change, and a deleted user's other devices could still call the API in that time.

## Decision
`AuthSetup` sets `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero`, so the stamp is checked on every
authenticated **user** request. Guests are skipped: they have no Identity account (`OnValidatePrincipal`, ADR-0023).

On each signed-in user request the validator:
1. loads the user by id (a primary-key lookup on `AspNetUsers`);
2. rebuilds the principal, which also reads the user's claims (one more indexed query);
3. renews the session cookie, so the response carries a `Set-Cookie` of a few hundred bytes.

## Consequences
+ A password change ends the user's other sessions on their next request, and a deleted account's sessions answer 401
  at once, as §14 says. Tests show both (`ProfileEndpointTests`).
- Roughly 1–2 small indexed queries and one cookie re-encryption per signed-in request. This is estimated, not
  measured: at our scale (one API instance, ADR-0016) it is around a millisecond or two next to endpoints that already
  run several queries.
- Every user response sets the session cookie again. This is harmless (it already slides, ADR-0023) but noisier.
- SignalR is checked once when it connects, not per message. An open hub connection survives a password change or
  deletion until it reconnects. It can only receive room events; votes and other writes go over HTTP (ADR-0015) and
  are checked.

## Revisit when
The extra lookups show up in profiling, or the API scales out (ADR-0016). Raise the interval then, for example to one
minute: this removes almost all of the cost, and sessions still end within that minute. §14's "at once" would then read
"within a minute". Changing it is one line in `AuthSetup`, plus that contract note.

## Alternatives rejected
- **Identity's default of 30 minutes:** a stolen session would keep working for up to half an hour after the owner
  changes the password, against §14.
- **A short interval (e.g. 1 minute) from the start:** cheaper, but not "at once". Kept as the first step if the cost
  ever matters.
- **A server-side session store or revocation list:** a new table and lookups of its own, to save one primary-key read.
