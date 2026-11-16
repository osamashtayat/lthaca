# Library

"My Cinema" (api-contract.md §7, ADR-0019): `UserFilm` (liked / watchlist / seen / 0.5–5 rating in half steps, Domain/Library).
  Stored as a count of half stars (1–10, ADR-0024); `UserFilm` alone converts (`ToRating`, `IsValidRating`), requests
  validate with `[HalfStarRating]`, and `sort=rating` orders on the stored `RatingHalfStars`.

- `LibraryService`: list (shelf, sort, genre, paged), summary, get, put (rating ⇒ seen; nothing set deletes the row) and
  the idempotent watchlist pair, always for the signed-in user from `ICurrentActor`.
- DTOs in `LibraryDtos.cs`, enums in `LibraryEnums.cs`. No `LibraryErrors`: the only failures are `FilmErrors.NotFound`
  and `AuthErrors.Unauthenticated`.
- A `UserFilm` keeps its film from being purged; the sync refreshes it instead (`CatalogSyncService`).
