# ADR-0009: Plain HTTP locally; ports chosen to coexist with FlowOps (deviation from FlowOps)

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
FlowOps serves HTTPS on 8443 with a self-signed certificate because its cookies are `__Host-`/Secure and antiforgery requires HTTPS. Ithaca has no auth yet, 95% of users are on phones, and self-signed certificates make phone testing painful. FlowOps also already uses ports 5432 and 5180 on the developer's machine.

## Decision
- Docker serves `http://localhost:8080` (nginx: SPA + `/api` + `/hubs`), the API has no published port.
- Local dev: DB `localhost:5433`, API `http://localhost:5280`, Vite `http://localhost:5273`.
- No HTTPS redirection in the API; TLS terminates in front of nginx in a real deployment. Revisit with the auth design (cookies/antiforgery may require HTTPS locally again).

## Consequences
+ Phones on the LAN can open the site; both projects run side by side.
- Not production-like for cookie security until auth is designed.

## Alternatives rejected
Copying FlowOps' TLS setup now (friction with no current benefit).
