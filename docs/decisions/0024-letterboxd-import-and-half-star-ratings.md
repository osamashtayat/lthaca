# ADR-0024: Letterboxd import and half-star ratings

- **Status:** Accepted (owner, 2026-11-13), decisions 1–9 and the answers to Q1–Q5 (see Outcome)
- **Date:** 2026-11-13

## Context
"By taste" needs history, and new users have none in Ithaca. Many of them have years of it on Letterboxd. The backlog
already reserved an import (`ImportJob`, BE-15a / BE-15 / FE-14, the "Import from Letterboxd" board) but left how it
works open, and proposed rounding Letterboxd's half stars to Ithaca's whole 1–5 rating.

Facts that shape the decision (checked on Letterboxd's own help pages, 2026-11-13):
- Letterboxd has no open API for this. Members export their data themselves: Settings → **Data** on the website
  (`letterboxd.com/user/exportdata/`, button "Export Data"), which downloads **one ZIP of CSV files**. Letterboxd's
  help centre says the export "can only be done via desktop browser" (not in its apps).
- The export bundles the whole account: films, ratings, watchlist, likes, diary, reviews, comments, lists, deleted
  content and the profile (which holds the member's email address).
- Its rows carry title, year and a Letterboxd link, **no TMDB id**. Letterboxd ratings are 0.5–5 in steps of 0.5.
- Our catalog holds about 843 popular films (launch decision L6), so many imported films aren't in it.
- TMDB is never called on a request path (ADR-0003); jobs are plain `BackgroundService`s (ADR-0008); referenced films are
  refreshed, never purged (ADR-0019); users' own data may feed AI, TMDB text may not (ADR-0017).

## Decision

### Getting the data
1. **File upload only.** The user exports from Letterboxd and uploads the ZIP (or one CSV from it) to Ithaca. No
   scraping, no Letterboxd sign-in, no stored Letterboxd credentials or tokens.
2. **What we read, and nothing else:**

   | Export file | Becomes |
   |---|---|
   | `watched.csv` | `seen` |
   | `ratings.csv` | `rating`, which implies `seen` (A8) |
   | `watchlist.csv` | `onWatchlist` |
   | `likes/films.csv` | `liked` |

   Reviews, comments, the diary, lists, `deleted/` and `profile.csv` (it contains the email) are never opened. Columns
   other than name, year, Letterboxd URI and rating are ignored. The file names and columns are Letterboxd's current
   export layout, not a documented format; BE-15 pins them with a real export fixture and fails clearly on a layout it
   doesn't recognise.
3. **The uploaded file is never stored.** The upload request reads the four files in memory, writes the parsed rows of
   the `ImportJob` (`ImportItem`s: title, year, Letterboxd URI, the four flags, rating) and drops the file before it
   answers. Nothing is written to disk or object storage. Items are deleted when the job finishes, except the
   not-found ones of the user's latest job (the "not found" list); deleting the account deletes all of it.
4. **Films missing from our catalog are looked up on TMDB in the background**, by title and year, in the import job,
   never on a request path (ADR-0003). A found film is added to `films` the same way the catalog sync adds one (one code
   path) and, once a `UserFilm` references it, kept under ADR-0019. Its Wikidata facts and embedding follow in the next
   catalog sync, like any new film. Rows still unmatched are reported to the user as "not found". A Letterboxd URI →
   TMDB id match is remembered (our own mapping, ids only) so re-imports don't search again.

### Ratings
5. **Half stars are kept, everywhere.** Ithaca's own rating becomes **0.5–5 in steps of 0.5** for the whole product, not
   only imports; imported ratings are copied as they are. Storage: an integer count of half stars, **1–10**, in the
   database; the API speaks 0.5–5. Reasons: exact values (no binary-fraction or rounding questions), the check
   constraint stays a plain range, sorting is unchanged, and Letterboxd's own 1–10 scale (`Rating10`) maps 1:1. The
   conversion lives in one place in `UserFilm`. Any other API value is 400 `validation` (`errors.rating`).
   This changes merged work (BE-05, FE-02, FE-04, FE-07): new tasks BE-20 and FE-20.

### Merging and safety
6. **Never remove; Ithaca wins.** An import only adds: it sets `seen`, `liked` and `onWatchlist` to true where the file
   says so and never sets anything to false or deletes a row. A rating is written only where the `UserFilm` has none; a
   film already rated in Ithaca keeps that rating.
7. **Re-import is safe.** A newer export runs the same rules: one `UserFilm` per user and film (the existing unique key),
   so nothing is duplicated and only what's new is added. One active job per user at a time.
8. **Hostile files are expected.** Size limit on the upload; ZIP-bomb limits (entry count, total and per-entry
   uncompressed size of the files we read, compression ratio, counted while reading, not trusted from the headers); type checked by
   content (ZIP signature or a UTF-8 CSV with a recognised header), never by extension or `Content-Type`; nested
   archives ignored; a row cap per import. Limits are config (`ImportOptions`) with defaults in §13 of the contract.
   Creating imports is rate-limited per user (BE-19). Titles and rows are never logged (they reveal what someone
   watches).
9. **Taste.** Imported likes and ratings are ordinary `UserFilm` data, so they feed the `TasteProfile` (BE-14). They are
   the user's own data, allowed for AI under ADR-0017; only Wikidata facts of the films reach the embedding model.

### Where users meet it
- **By taste with no or too little history** shows an "Import your Letterboxd" screen instead of a dead end (FE-13).
  Whether there is enough history is decided by the server (BE-14a exposes it), never by the screen.
- That screen and `/import` have a **"?" help** that opens short, phone-friendly steps for getting the ZIP, including
  that Letterboxd's export needs the desktop version of its website.
- The import itself is `/import` (FE-14), reachable from My Cinema (its link exists) and from Profile (FE-15) (Q3).

The contract is [api-contract.md §13](../api-contract.md#13-import-letterboxd) and the §7 rating change; the tasks are
OPS-12, BE-15a, BE-15, BE-20, FE-13, FE-14, FE-20, with scope added to BE-14a, BE-14, BE-16a, BE-19 and FE-15, in
[the backlog](../backlog.md). OPS-12 also wrote the design into api-contract.md (backend-owned) and
frontend-conventions.md (frontend-owned) because the owner decided both sides at once; the backend and frontend tasks
above own those sections from here on ("as built").

## Consequences
+ New users can reach "By taste" in minutes with history they already have; nothing they did in Ithaca is overwritten.
+ No third-party credentials, no scraping, no stored files; the uploaded email address is never read.
+ The catalog grows with what users actually watch, and those films stay (ADR-0019).
+ One rating scale in the product and in Letterboxd: no lossy rounding, no "why is my 3.5 a 4?".
- Already-merged code changes: a migration on `user_films`, DTO types, `StubRating` and its users (BE-20, FE-20).
- Imports cost TMDB calls (one search, one details call per missing film); a large history takes minutes, so the job
  shows progress and the user can leave and come back.
- Phone users must switch their browser to the desktop site (or use a computer) for Letterboxd's export: friction we
  can only explain, not remove.
- Title + year matching can miss or, rarely, mismatch a film; misses are listed, and a wrong match is fixable on the
  film's page like any library entry.
- Films added by an import have no Wikidata facts or embedding until the next sync, so taste ignores them for up to a day.

## Alternatives rejected
- **A Letterboxd API or scraping.** There is no open API; scraping profiles breaks their terms, is brittle and would
  need the user's username or credentials.
- **Rounding half stars to 1–5.** Loses information the user chose deliberately and makes a re-import look like it
  changed their ratings.
- **Storing the rating as a decimal column.** Works, but invites 3.49-style values and float comparisons; a 1–10
  integer is exact and maps to Letterboxd's own `Rating10`.
- **Matching only the existing catalog.** With ~843 films most of a typical history would be "not found".
- **Calling TMDB during the upload request.** Slow, fails with TMDB, breaks ADR-0003.
- **Keeping the uploaded file** (for re-processing or support). It holds reviews, diary and the email address; we don't
  need them, so we don't keep them.
- **Import overwrites Ithaca data, or mirrors deletions.** Surprising and destructive; an export is a snapshot, not a sync.

## Open questions (owner) — answered, see Outcome
| Id | Question | Recommendation | Assumed until decided |
|---|---|---|---|
| Q1 | Guests and import: guests have no library (ADR-0012) and solo needs an account (A3). Where would a guest's import live? (a) create an account first, then import; (b) a temporary, room-scoped import used only for that room's group taste and deleted with the room; (c) no import for guests. | **(a)**: keeps ADR-0012 intact, one import path, and guest upgrade already keeps their room votes. (b) needs a second, room-scoped store, a new retention rule and group-taste logic for half-anonymous data; (c) is a dead end for exactly the people we want to convert. | (a): `/import` sends guests to sign in or create an account |
| Q2 | How much history is "enough" for By taste without asking for an import? | At least **10 films with a positive signal** (liked, rated ≥ 3.5, or "loved it"), a config value BE-14 can tune | 10, owned by BE-14a/§12 |
| Q3 | Extra entry point: My Cinema, Profile, or both? | **Both**: My Cinema already links to `/import`; Profile gets a "Letterboxd · Import" row | both |
| Q4 | Grow the catalog first (L6) so imports match more of it locally? | Not a blocker: the import adds missing films itself. A bigger catalog (L6) just means fewer TMDB lookups per import | import does not wait for L6 |
| Q5 | A re-import whose rating differs from an earlier **imported** rating (the user re-rated on Letterboxd): update it? | **Yes**: remember where a rating came from (Ithaca or import); later imports may update imported ratings, never Ithaca ones. One extra column in BE-20's migration | No: any existing rating wins (decision 6 as written) |

## Outcome (owner, 2026-11-13)
The owner accepted the assumption column for all five questions. These are now decisions:
- **Q1:** guests create an account before importing; `/api/imports` stays user-only and `/import` sends guests to sign in
  or create an account (guest upgrade keeps their room votes). No room-scoped import.
- **Q2:** By taste is ready with at least **10** films liked, rated ≥ 3.5 or "loved it" (a config value; BE-14a/§12
  exposes readiness, BE-14 owns the rule).
- **Q3:** the import is reachable from both My Cinema and Profile.
- **Q4:** the import does not wait for a bigger catalog (L6); it adds missing films itself.
- **Q5:** **no**: any existing rating wins on a re-import, whether it was made in Ithaca or imported earlier (decision 6
  as written). No rating-source column; the recommendation to track the source is not taken.
