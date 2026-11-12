# ADR-0016: One API instance for now; what to add before scaling out

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
The nightly sync is a plain `BackgroundService` (ADR-0008) and SignalR keeps room groups in process memory. Both assume one API instance.

## Decision
Stay single-instance. Before running a second API replica:
- **Catalog sync:** take a Postgres advisory lock (`pg_try_advisory_lock`) at the start of each run so only one instance syncs, or move the job to a dedicated worker container (same image, `CatalogSync:Enabled=true` only there).
- **SignalR:** add a backplane (Redis) or a managed SignalR service so room events reach connections on every instance.

## Consequences
+ Nothing extra to run now.
- Scaling out is not a config change; it needs both items above.

## Alternatives rejected
Adding Redis and a lock now (no load requiring it).
