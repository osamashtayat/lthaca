# Glossary

Use these names exactly in code (types, tables, routes, UI copy where it fits). Snake_case plural for tables.

| Term | Meaning | Notes |
|---|---|---|
| `User` | A registered person. | Auth design not decided yet. |
| `Guest` | Someone who joined a room from a shared link without an account. | Can vote in that room only. |
| `Film` | A movie in **our** catalog, synced from TMDB, with an optional embedding and the model that made it. | Public id is `TmdbId`. Table `films`. Refreshed at least every 180 days (TMDB terms); films user data references are refreshed by id and never purged (ADR-0019). |
| `FilmFacts` | What Wikidata (CC0) says about a `Film` — genres, subjects, directors, cast, countries, languages, year, runtime, awards — and the embedding text built only from them. | The only film data AI may see (ADR-0017). Table `film_facts`, one row per matched film. |
| `FilmTrailer` | The cached answer to "what is this film's trailer?": the first YouTube trailer TMDB lists, or none. | Table `film_trailers`, one row per film; refreshed after 30 days, never kept past 180 (ADR-0003, ADR-0017). |
| `UserFilm` | A user's relation to a film: liked, watchlist, seen, personal rating (0.5–5 in half steps, shown as ticket stubs). No "watching" state. | Powers "My Cinema" (Library). Stored as half stars 1–10 (ADR-0024). |
| `Room` | A group session with one shared deck, joined by link or code. | Stored in the DB (table `rooms`, codes like `POP-428`); real-time via RoomHub. |
| `RoomMember` | A `User` or `Guest` in a `Room`. | Host is a member with extra rights: the one present longest (BE-10). Leaving keeps the row (`left_at`). Table `room_members`. |
| `Deck` | A user's solo deck: how it was started (just swipe, mood, taste) and the `DeckItem`s dealt so far. | Table `decks`. A room's shared deck is the room's (BE-11). Proposed by BE-08 (A12). |
| `DeckItem` | One card (a `Film`) at a position in a deck — solo or a room's shared deck. | Group "Just swipe" adds them in batches. |
| `Swipe` | A user's gesture on a card in their own `Deck`: right, left or up (+ optional loved it / not for me). | Table `swipes`, one per `DeckItem`, keyed by the client's `swipeId`. A room member's is a `Vote`. Proposed by BE-08 (A12). |
| `Vote` | A member's swipe on a `DeckItem`: right (I'd watch it), left (not for me), up (seen it → loved it / not for me). | Same three gestures everywhere. |
| `TasteProfile` | A user's (or room's) taste vector in the film-embedding space, plus what built it. | Built after a few swipes or from history. |
| `MoodQuery` | A mood request: tapped moods and/or free text, and the structured filters the LLM parsed from it. | Output of `mood-parse.vN`. |
| `ImportJob` | One upload of a user's Letterboxd export (ZIP or one CSV from it), processed in the background: progress, counts, and the films it couldn't match. Its parsed rows are `ImportItem`s; the file itself is never stored. | Feeds `UserFilm`; only adds, never removes; existing Ithaca choices win (ADR-0024). Tables `import_jobs`, `import_items`. |
| `LetterboxdFilmLink` | A Letterboxd film URI and the TMDB id an import matched it to (ids only), shared by all users so re-imports don't search TMDB again. | Written by the import job (ADR-0024). Table `letterboxd_film_links`. |

Other words used in docs and UI:
- **Deck** — the swipeable stack. **Sampler** — the varied first cards of "Just swipe" before a `TasteProfile` exists.
- **Just swipe / By mood / By taste** — the three ways to start a deck. **Solo / Group** — alone or in a `Room`.
- **Match** — every member said yes to the same film. **Best compromise** — the highest-scoring film when there's no match.
- **Hard no** — a member's explicit exclusion; must never appear in the group result.
- **Taste picks** — the 10–20 films someone picks as "films I love" when they have too little history (ADR-0026): a
  user's become likes in My Cinema; a guest's are kept on their `RoomMember` for that room only.
- **My Cinema** — the Library screen. **Tonight** — the home screen.
