# ADR-0019: Films users reference are refreshed, never purged

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
TMDB's terms forbid keeping TMDB data longer than 6 months, so the catalog sync deletes films not refreshed within
`CatalogSync:MaxAgeDays` (≤ 180). Once `UserFilm`, `DeckItem` and `Vote` point at films, deleting a film would break a user's
library, a room's history, or a taste profile.

## Decision
- **Refresh by reference:** each sync also re-fetches (by TMDB id) every film referenced by user data, whether or not it is
  still in TMDB's popular pages. Referenced films therefore never pass the age limit.
- **Purge only orphans:** the age-based purge deletes only films no user data references.
- **Safety net:** every foreign key to `films` uses `ON DELETE RESTRICT`; a purge bug fails loudly instead of cascading.
- If TMDB stops knowing a referenced film (404), keep the row but clear its TMDB-derived fields (overview, poster) and mark it
  unavailable; our own ids and the user's data stay.
- Built with the first feature that references films (`UserFilm`); `CatalogSyncService` gets the refresh step then.

## Consequences
+ Libraries and room history survive catalog churn; TMDB's 6-month limit is still met.
- The nightly sync grows with the number of referenced films (one details call each); fine at our scale, batch later if needed.

## Alternatives rejected
Cascade delete (silent data loss); keeping stale TMDB data for referenced films (breaks the 6-month rule); storing only ids
and fetching details live (live TMDB calls on the request path, ADR-0003).
