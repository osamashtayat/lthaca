# ADR-0003: TMDB is synced nightly into our catalog, not called live

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Decks need fast, filterable, embeddable candidates; TMDB has rate limits and latency, and the LLM must only ever see films we know exist.

## Decision
- A nightly job (`CatalogSyncJob` → `CatalogSyncService`) upserts a catalog of films from TMDB into `films`, with embeddings.
- At runtime, all candidates come from our database. TMDB is called only for details we don't store (e.g. trailers), and those responses are cached.
- `ITmdbClient` is the only TMDB access; `Tmdb:Client` selects `Fake` (built) or `Http` (not built yet).

## Consequences
+ Predictable latency, no rate-limit failures on the swipe path, embeddings available for every candidate.
+ The LLM is given ids from our table, so validating its output is a set check.
- Catalog freshness is up to a day old; new releases appear after the next sync.
- We store a subset of TMDB data and must respect TMDB's terms (attribution, caching limits).

## Alternatives rejected
Live TMDB search per deck (slow, rate-limited, no embeddings); a full TMDB mirror (unnecessary volume).
