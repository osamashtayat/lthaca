# ADR-0008: Plain BackgroundService for the nightly sync; no job framework

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
FlowOps ADR-0010 chose no background processing in its phase 1. Ithaca needs exactly one scheduled job: the nightly TMDB catalog sync.

## Decision
- `CatalogSyncJob : BackgroundService` in `Infrastructure/Jobs/`, enabled by `CatalogSync:Enabled`, runs daily at `CatalogSync:RunAtUtc`. It only schedules and calls `CatalogSyncService` in a DI scope.
- No Hangfire/Quartz until there are several jobs, retries or a dashboard need.

## Consequences
+ No dependency, no extra tables.
- Single instance assumption: with several API replicas the job would run on each; add a lock or a dedicated worker before scaling out.
- No persisted job history; logs only.

## Alternatives rejected
Hangfire/Quartz (premature); an external cron container (more infrastructure for one job).
