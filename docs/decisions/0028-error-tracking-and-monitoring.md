# ADR-0028: Error tracking and monitoring with Sentry, plus an uptime monitor

- **Status:** Accepted (owner, 2026-11-16; OPS-17)
- **Date:** 2026-11-16
- **Decides:** L7 (backlog, Launch decisions). Replaces its earlier recommendation, "logs only".

## Context
Before launch the owner wants three things: every error caught, with exactly what happened, kept somewhere searchable;
to be told when the site is down; and to be told when something fails quietly. As of BE-17, the API writes JSON logs to
the container console (`AddJsonConsole`) and `GET /api/health` only says the process is up. The logs disappear when the
container is recreated. Nothing alerts anyone. Frontend errors on users' phones are never seen.

## Decision
**Sentry** (sentry.io, organization `osama-vz`, **EU data region**) tracks errors and keeps logs. There is one project
per platform:
- `ithaca-server` (ASP.NET Core);
- `ithaca-frontend` (React).

**An uptime monitor** checks the public site from outside. OPS-09 sets it up once there is a URL: Better Stack's free
tier, or Sentry Uptime if the plan covers it.

What each part sends:
- **Backend (BE-21):**
  - every unhandled exception, as an issue;
  - everything logged at Error or above, from requests, background jobs (catalog sync, imports) and the room hub;
  - Warning-and-above logs to Sentry Logs, so handled failures (AI, TMDB, Wikidata, Voyage, Resend) are searchable
    and can be counted;
  - request traces;
  - a **cron check-in** from the nightly catalog sync, so a sync that never runs raises an alert.
- **Frontend (FE-21):**
  - unhandled errors and React render errors, with an error boundary that shows a friendly screen;
  - page-load and navigation traces;
  - ~~Session Replay of the moments before an error~~ — dropped in FE-21 (see Amendments).

**Privacy (owner: "only the internal id"):**
- `SendDefaultPii` is off.
- A user is reported by their internal random id only: no email, no name, no IP address.
- Never sent: request bodies, cookies, auth or antiforgery headers, or mood text.
- Log messages already follow backend-conventions.md: no emails, mood text or keys.
- Npgsql error details stay off, so constraint errors can't carry values.

**Switches and configuration:**
- Off unless a DSN is configured (`Sentry:Dsn` for the API, `VITE_SENTRY_DSN` for the SPA). Tests, CI and local dev
  send nothing by default.
- A DSN is not a secret, but it lives in environment config (`.env`, user-secrets, CI variables), never in
  `appsettings*.json`.
- Trace sampling is 100% in Development and 10% in Production; errors are never sampled.
- Every event carries the environment, so test errors from a laptop never page anyone.

**Health:**
- `GET /api/health` stays the liveness check (the process is up).
- `GET /api/health/ready` also checks the database. Docker and the uptime monitor use it.
- The `api` container gets a Docker healthcheck, `restart: unless-stopped`, and log rotation.

## Consequences
+ Errors from the server, the background jobs and users' phones reach one place with stack traces, and the owner is
  emailed on a new issue. Warnings are searchable without SSH access to the server.
+ A silent failure is visible: a sync that didn't run (cron check-in), the AI quietly falling back (warning counts), or
  the site down (uptime monitor).
- Sentry is a new data processor for user data (pseudonymous ids, device info, masked replays). The Privacy text (L5)
  must name it, with its EU data region.
- Free-tier quotas apply (errors, replays, logs, cron monitors). Sampling and Warning-and-above keep usage low. If the
  quota is reached, Sentry drops events; the app is unaffected.
- The frontend bundle grows by the Sentry SDK (measured in FE-21).
- Production stack traces from the SPA need source maps uploaded at build time, with a Sentry auth token in CI. That
  is part of OPS-07; until then frontend traces are minified.
- Alert rules (e.g. many AI fallbacks per hour) and the uptime monitor need the deployed site: OPS-09.

## Revisit when
The free quota is regularly exceeded, the owner wants a single vendor for uptime and errors, or the Privacy review (L5)
needs data kept elsewhere.

## Amendments
- **2026-11-16 (FE-21): no Session Replay.** The replay recorder stores full page URLs (e.g. `/reset-password?token=…`,
  `?search=…`) in recording events that the SDK can't modify before sending, so it would break the privacy rule above.
  Errors, failed route loads and traces are sent; replay can come back once tokens no longer sit in URLs and Sentry's
  server-side scrubbing of recordings is verified. Also from FE-21: the IP-derived location is removed by a project
  data-scrubbing rule (`$user.geo.**`), and both projects have "prevent storing IP addresses" on.

