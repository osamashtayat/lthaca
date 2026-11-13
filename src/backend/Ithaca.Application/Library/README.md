# Library

"My Cinema" (api-contract.md §7, ADR-0019): `UserFilm` (liked / watchlist / seen / 1–5 rating, Domain/Library).

- `LibraryService`: list (shelf, sort, genre, paged), summary, get, put (rating ⇒ seen; nothing set deletes the row) and
  the idempotent watchlist pair, always for the signed-in user from `ICurrentActor`.
- DTOs in `LibraryDtos.cs`, enums in `LibraryEnums.cs`. No `LibraryErrors`: the only failures are `FilmErrors.NotFound`
  and `AuthErrors.Unauthenticated`.
- A `UserFilm` keeps its film from being purged; the sync refreshes it instead (`CatalogSyncService`).
