# API contract (first wave)

**What this is:** the *design* contract between backend and frontend for the first-wave features: auth, solo deck and
swipes (incl. mood), rooms and votes, library (My Cinema), film details, browse. Once backlog task **BE-01** lands, the
*executable* contract is `contracts/openapi.json` (generated from the code) and the frontend types come from it
(`npm run api:types`). The two must agree; §10 says how to change either. Decision: ADR-0021 (contract-first).

Not repeated here: the error pipeline (docs/backend-conventions.md → Result and errors), real-time events
(`contracts/realtime-events.json` + docs/realtime.md), domain names (docs/glossary.md), AI rules (docs/ai.md).
Second-wave features (taste, import, profile, Tonight) get their contract sections in their own backlog task: §12 Taste
(BE-14a); §13 Import was designed with its decision (OPS-12, ADR-0024) and BE-15a makes it executable; §14
Profile (BE-16a).

Places where the docs, ADRs or design leave a gap are marked **⚠ Open** with the assumption this contract makes.
They are collected in §11 and in the backlog's open questions; the owner confirms or changes them.

---

## 1. Conventions

- JSON camelCase; enums as **strings** (`JsonSetup`: integers are rejected); numbers strict; ids are UUID strings except
  films, whose public id is the TMDB id (`tmdbId`, integer); timestamps ISO-8601 UTC (`2026-11-12T21:04:00Z`).
- Everything under `/api`, same origin (Vite proxy / nginx), cookies only, no tokens in JS (ADR-0012).
- Inputs are `…Request` records, outputs `…Dto` records (AGENTS.md §3). Create → `201` + `Location`; command without a
  body → `204`.
- **Paging** (lists): query `page` (1–10 000, default 1) and `pageSize` (1–100, default 20) from `PageRequest`; out of
  range → 400 `validation`. Response:
  ```ts
  type PagedResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number }
  ```
- **Idempotent writes:** swipes and votes carry a client-generated UUID (`swipeId`, `voteId`). Re-sending the same id is a
  no-op success; mutations never auto-retry on the client (docs/frontend-conventions.md → Data).
- **Access** column values: **anon** (no session needed), **user** (signed-in `User`), **member** (a `RoomMember` of that
  room: a user member or a guest whose cookie is scoped to that room), **host** (the member who created the room).

## 2. Errors

ProblemDetails exactly as built (`type`, `title`, `status`, `detail`, `code`, `traceId`; 400 adds `errors: { field:
[messages] }` with camelCase field paths). The frontend shows `toUserMessage(code)`, never `detail`.

Every code below must exist in `Ithaca.Domain/Errors/ErrorCodes.cs`, `Ithaca.Api/Errors/ErrorStatusCodes.cs` and
`src/frontend/src/shared/api/problem.ts` **before** an endpoint can return it (`ErrorStatusCodesTests` fails otherwise).
BE-01 adds all of them at once, so mocks and screens can use them from day one.

| Status | `code` | Meaning |
|---|---|---|
| 400 | `validation` | DataAnnotations or business validation (`errors.{field}`) — exists |
| 400 | `auth.antiforgery_failed` | unsafe request without a valid `X-XSRF-TOKEN` header |
| 401 | `auth.unauthenticated` | no session, expired session, or a guest calling a user-only endpoint |
| 401 | `auth.invalid_credentials` | wrong email/password, also while locked out (never reveals which) |
| 403 | `auth.forbidden` | signed in but not allowed (generic) |
| 403 | `room.not_member` | caller isn't a member of this room |
| 403 | `room.not_host` | host-only action |
| 404 | `not_found`, `film.not_found` | exist |
| 404 | `deck.not_found` | no such deck **for this user** (another user's deck is also 404) |
| 404 | `deck.item_not_found` | the deck item isn't in this deck / room |
| 404 | `room.not_found` | no open or recent room with that code |
| 409 | `auth.email_taken` | register with an email that has an account |
| 400 | `auth.invalid_reset_token` | password reset with a token that is malformed, unknown, expired or already used, never saying which (§4) — BE-18a |
| 409 | `deck.already_swiped` | a different `swipeId` for an item already swiped |
| 409 | `room.already_voted` | a different `voteId` for an item this member already voted on |
| 409 | `room.closed` | the room is closed (or finished) and takes no joins, votes or changes |
| 409 | `room.full` | the room already has 8 members |
| 409 | `room.invalid_state` | action not allowed in the room's current `status` (e.g. vote in the lobby, start twice) |
| 409 | `room.no_result` | result requested before there is one |
| 413 | `request.too_large` | the request body is over the server's limit for that endpoint (the pipeline refuses it before any service runs; mapped in `ProblemDetailsExceptionHandler`) — BE-15a |
| 400 | `profile.wrong_password` | password change or account deletion with a wrong current password, also while locked out (§14) — BE-16a |
| 404 | `import.not_found` | no such import **for this user** (another user's import is also 404) — BE-15a |
| 400 | `import.invalid_file` | not a Letterboxd export: wrong type, unreadable, no recognised file or header, or a ZIP over the entry-count / ratio limits (§13) — BE-15a |
| 409 | `import.already_running` | the user already has a `queued` or `running` import — BE-15a |
| 413 | `import.file_too_large` | the files read from the upload unpack to more than the limit (§13) — BE-15a |
| 422 | `import.nothing_to_import` | a valid export with no rows in the files we read — BE-15a |
| 422 | `import.too_many_films` | more distinct films than the per-import cap (§13) — BE-15a |
| 422 | `deck.no_yeses` | top 3 requested before any right swipe |
| 422 | `taste.not_ready` | a deck (or, for its host, a room) by taste without enough history (§12) — BE-14a |
| 422 | `mood.unclear` | mood text isn't a mood (unclear or off-topic) and no chips were tapped; nothing created (ADR-0022) |
| 422 | `mood.text_unavailable` | we couldn't read the mood text right now (the AI failed or the daily AI budget is spent) and no chips were tapped; nothing created. The screens say "mood text isn't available right now, pick a few moods instead" — BE-19 |
| 429 | `rate.limited` | too many requests of this kind from one person (or, without a session, one IP address); the response has a `Retry-After` header in whole seconds — BE-19 (§2 as built below) |
| 422 / 502 / 503 | `ai.refused` / `ai.invalid_output` / `ai.unavailable` | an AI call failed — exist (docs/ai.md); since BE-19 no endpoint returns them (mood text falls back, picks lose their reasons), only the catalog sync |
| 503 | `tmdb.unavailable` | trailer lookup failed and nothing is cached — exists |

**As built (BE-19): rate limits.** Over a limit the request is refused before any service runs: 429 `rate.limited`,
ProblemDetails as usual plus `Retry-After: <seconds>` (time left in the window, at least 1). Each signed-in user or guest
counts on their own (their session's id); a request without a session counts by client IP (`X-Forwarded-For` is read only
from a trusted proxy, `RateLimits:TrustedProxyNetworks`). A window opens with a person's first request and holds the
limit until it ends; refused requests don't count, every other answer does (also a 4xx). Defaults (`RateLimits` in
`appsettings.json`):

| Policy | Endpoints | Default |
|---|---|---|
| `auth` | `POST /api/auth/register`, `sign-in`, `forgot-password`, `reset-password` | 20 per 15 min |
| `room-join` | `POST /api/rooms/{code}/join` | 20 per 10 min |
| `deck-create` | `POST /api/decks` (any source; its mood AI call included) | 30 per hour |
| `import-create` | `POST /api/imports` (ADR-0024) | 5 per day |
| `ai` | `PUT /api/rooms/{code}/mood`, `GET /api/decks/{deckId}/top`, `GET /api/rooms/{code}/result` | 60 per 10 min |

**As built (BE-17):** every endpoint is live, so the stub code (501 `server.not_implemented`), `ContractStub` and
`[ContractStub]` are deleted. A later contract-first task that needs stubs again (ADR-0021) re-adds them with its first
stub and lists the code here.

## 3. Shared shapes

```ts
type SwipeDirection = "right" | "left" | "up"        // I'd watch it · not for me · seen it (glossary: Vote)
type SeenVerdict    = "loved" | "notForMe"           // only with "up"; null = "skip rating"
type Mood           = "cozy" | "laugh" | "keepGuessing" | "makeMeFeel" | "takeMeAway" | "surprise"
type RuntimeLimit   = "under90" | "upTo2h" | "any"
type HardNo         = "horror" | "subtitles" | "gore" | "musicals" | "sadEndings"

// Existing (GET /api/films/{tmdbId}); BE-06 adds the fields marked +, additively.
type FilmDto = {
  tmdbId: number; title: string; releaseYear: number | null; overview: string | null; posterPath: string | null;
  runtimeMinutes: number | null; genres: string[];
  backdropPath: string | null;        // + hero image on details A
  voteAverage: number | null;         // + TMDB score, e.g. 8.0 ("★ 8.0" on cards and details)
  voteCount: number | null;           // + "12k ratings"
  originalLanguage: string | null;    // + ISO 639-1, for the mood "Language" filter and "subtitles" hard no
}
// Lists (browse, library, results): no overview.
type FilmSummaryDto = Omit<FilmDto, "overview" | "backdropPath" | "voteCount" | "originalLanguage">

type SwipeDto    = { direction: SwipeDirection; seenVerdict: SeenVerdict | null }
type DeckItemDto = {
  id: string; position: number; film: FilmDto;
  swipe: SwipeDto | null;   // the caller's own swipe or vote
  onWatchlist: boolean;     // the caller's watchlist state, for the card's watchlist button (A2); false for guests
}
```
Posters/backdrops are TMDB image paths; the frontend builds the URL (TMDB image CDN) and always shows the footer
attribution (ADR-0017). Mood/hard-no **labels** are UI copy (the canvas wording) and live in the frontend; the server owns
the meaning.

## 4. Auth (ADR-0012)

Cookie session (ASP.NET Core Identity, HttpOnly, SameSite=Lax, Secure in Production). Unauthenticated API calls get
**401 JSON, never a redirect**. Antiforgery copies FlowOps: the server sets a JS-readable `XSRF-TOKEN` cookie; the client
(`shared/api/client.ts`, BE-02/FE-03) echoes it in `X-XSRF-TOKEN` on every POST/PUT/PATCH/DELETE. No endpoint is exempt:
`GET /api/auth/me` issues the cookie even when it answers 401, and every call that changes the principal (register,
sign-in, sign-out, room join, and a guest's room leave) re-issues it.

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `GET /api/auth/me` | anon | — | 200 `CurrentUserDto` (user or guest) | 401 `auth.unauthenticated` |
| `POST /api/auth/register` | anon | `RegisterRequest` | 201 `CurrentUserDto`, signed in | 400, 409 `auth.email_taken` |
| `POST /api/auth/sign-in` | anon | `SignInRequest` | 200 `CurrentUserDto` | 400, 401 `auth.invalid_credentials` |
| `POST /api/auth/sign-out` | user or guest | — | 204 (cookie cleared) | — |
| `POST /api/auth/forgot-password` | anon | `ForgotPasswordRequest` | 204, always | 400 `validation` |
| `POST /api/auth/reset-password` | anon | `ResetPasswordRequest` | 200 `CurrentUserDto`, signed in, new `XSRF-TOKEN` | 400 `validation` / `auth.invalid_reset_token` |

```ts
type RegisterRequest = { email: string; password: string; displayName: string }  // password 8–128; name 1–40
type SignInRequest   = { email: string; password: string }
type ForgotPasswordRequest = { email: string }                     // a valid email, ≤ 256
type ResetPasswordRequest  = { token: string; newPassword: string } // token ≤ 1024, opaque; new password 8–128
type CurrentUserDto  = {
  kind: "user" | "guest"; id: string; displayName: string;
  email: string | null;      // users only
  roomCode: string | null;   // guests only: the one room this principal is valid for
}
```
- Lockout after 5 failed sign-ins for 15 minutes; a locked account answers exactly like a wrong password.
- **Guest upgrade (ADR-0012):** register or sign-in while holding a guest cookie moves that guest's `RoomMember` (and its
  votes) to the user; the guest cookie is replaced by the user cookie.
- Google/Apple sign-in ("Continue with Google" on the Welcome board) is later (proposed task P-02).
- ⚠ Open (A1): a guest who opens a second room's link gets a new guest identity for that room and loses the first
  (one room per guest principal). Assumed acceptable.

**As built (BE-02, ADR-0023):**
- Cookies: `ithaca_session` (HttpOnly, Lax, persistent 30 days sliding, Secure in Production), `ithaca_af` (antiforgery,
  HttpOnly) and `XSRF-TOKEN` (readable; send its value, URL-decoded, as `X-XSRF-TOKEN`).
- Every POST/PUT/PATCH/DELETE without a valid header answers **400 `auth.antiforgery_failed`**, sign-in included; every
  unsafe endpoint declares that 400 in `contracts/openapi.json`. A token is bound to the caller: after register,
  sign-in or sign-out, use the new `XSRF-TOKEN` from that response.
- `POST /api/auth/sign-out` without a session is still 204 (idempotent).
- User-only endpoints (Access **user** in §5–§7: decks, library, `POST /api/rooms`) answer 401 `auth.unauthenticated`
  to anonymous callers **and to guests**. Member and host rules arrive with the room tasks.
- Emails are matched case-insensitively and trimmed; display names are trimmed.

### Password reset (BE-18a)

"Forgot password?" on `/sign-in` and the emailed link's page `/reset-password?token=…` (FE-16). BE-18a adds the DTOs, the
code and 501 stubs; BE-18 the logic (`PasswordResetService` in `Application/Users`) and the email (Resend, L4).

**Rules** (owner, 2026-11-16; BE-18 built them; until then both answered 501, a code BE-17 removed, §2):
- **Forgot password:** always 204 for a well-formed email, whether or not it has an account, and for any caller (visitor,
  guest or user), so the answer never reveals an account. When it has one, the server emails a link to
  `<site>/reset-password?token=<token>`. Asking again sends a new link; earlier links stay valid until they expire or one
  is used. How often a caller may ask is BE-19's (rate limits).
- **The token** is opaque to the client: send it back exactly as it came in the link (URL-decoded). It is valid for
  **1 hour** and **one use**. Any password change (a reset, or §14's change) also invalidates every outstanding link.
- **Reset:** input validation first (400 `validation`), then the token; a bad token is 400 `auth.invalid_reset_token`
  with nothing changed. On success the new password is set (length is the only rule, as at registration), a sign-in
  **lockout is lifted** and the failure count cleared, every other session of the user ends (security stamp, ADR-0027),
  and the caller is **signed in** as that user: 200 `CurrentUserDto`, a session cookie and a new `XSRF-TOKEN`, as after
  sign-in. A guest caller is upgraded exactly as at sign-in (ADR-0012).

**As built (BE-18a, BE-18):** like every unsafe endpoint both need `X-XSRF-TOKEN` (no exemption, ADR-0023). Reset
re-issues `XSRF-TOKEN` on every answer it gives; forgot-password changes nobody and doesn't.
- **The token** is about 200 URL-safe characters (base64url, well under the 1,024 limit): the user id, the issue time and the account's security
  stamp, encrypted and signed with ASP.NET Data Protection. Any password change changes the stamp, which is what makes a
  link single-use and kills the others. The link URL-encodes it; send it back decoded, as it was.
- **The email** (Resend, L4): from `Email:From` (`Ithaca <no-reply@mail.ithacamovies.com>`), subject "Reset your Ithaca
  password", plain text + HTML from `Application/Email/Templates/password-reset.*`; the link is
  `Email:SiteUrl` + `/reset-password?token=…` (`http://localhost:5273` by default, `:8080` in Docker).
- **Same answer, every case:** 204 for an unknown email, and also 204 when the provider refuses or is down (logged, with
  no address or token; the user asks again). Response *timing* may differ (a real address waits for the send), which we
  accept: registration already answers 409 `auth.email_taken` for a known email. Logs never carry addresses or tokens.
- `Email:Provider=Fake` (tests, local development, the default) sends nothing; in Development it logs the email text so
  the link can be opened by hand. Production sets `Resend` and `Email__ApiKey` (a sending-only key).

## 5. Solo deck and swipes (incl. by mood)

A solo deck belongs to one user. "Just swipe" starts with a varied **sampler** and adapts; "By mood" starts from a
`MoodQuery`. Cards are `DeckItem`s at increasing positions; the client asks for more as it nears the end.

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `POST /api/decks` | user | `CreateDeckRequest` | 201 `DeckDto` (first batch), `Location: /api/decks/{id}` | 400, 401, mood: 422 `mood.unclear` / `mood.text_unavailable`, 429 `rate.limited` |
| `GET /api/decks/{deckId}` | user | — | 200 `DeckDto` | 401, 404 `deck.not_found` |
| `POST /api/decks/{deckId}/items` | user | — | 200 `DeckItemDto[]` (next batch; `[]` = nothing left) | 401, 404 |
| `POST /api/decks/{deckId}/swipes` | user | `SwipeRequest` | 204 | 400, 401, 404 `deck.not_found` / `deck.item_not_found`, 409 `deck.already_swiped` |
| `GET /api/decks/{deckId}/top` | user | — | 200 `DeckTopDto` | 401, 404, 422 `deck.no_yeses`, 429 `rate.limited` |

```ts
type DeckSource = "justSwipe" | "mood" | "taste"   // "taste": §12 (BE-14a), built by BE-14
type MoodInput  = {
  moods: Mood[];                 // 0–2 tapped chips
  maxRuntime: RuntimeLimit;
  language: string | null;       // ISO 639-1, null = any
  text: string | null;           // "Describe it your way", ≤ 280 chars — user text goes to the LLM, nothing else (docs/ai.md)
}
type CreateDeckRequest = { source: DeckSource; mood: MoodInput | null }   // mood required iff source = "mood" (400 errors.mood)
type MoodQueryDto = MoodInput & {
  id: string; filters: MoodFiltersDto;
  textIgnored: boolean;   // true: the text wasn't used, so only the chips were (ADR-0022)
  textUnavailable: boolean; // + BE-19: true when that's because we couldn't read the text right now (AI failure or the
                            //   daily AI budget), not because it wasn't a mood; implies textIgnored
}
type MoodFiltersDto = {           // the validated output of mood-parse (MoodParseOutput), after our own checks
  includeGenres: string[]; excludeGenres: string[]; maxRuntimeMinutes: number | null;
  minReleaseYear: number | null; maxReleaseYear: number | null; keywords: string[];
}
type DeckDto = {
  id: string; source: DeckSource; createdAt: string;
  mood: MoodQueryDto | null;
  items: DeckItemDto[];           // every item so far, in position order; unswiped ones have swipe = null
  swipeCount: number; yesCount: number;   // "14 swipes · 5 yeses"
}
type SwipeRequest = { swipeId: string; deckItemId: string; direction: SwipeDirection; seenVerdict: SeenVerdict | null }
type DeckTopDto = { swipeCount: number; yesCount: number; picks: TopPickDto[] }   // 1–3 picks, best first
type TopPickDto = { film: FilmDto; reason: string | null }                          // reason: the AI's one-liner (BE-13), null when none
```
Rules (server-side; the UI never decides them):
- `seenVerdict` only with `up` (else 400 `errors.seenVerdict`).
- **Library effects** of a swipe: `up` → `seen = true`; `up` + `loved` → also `liked = true`. `right` and `left` don't
  touch the library: a right swipe is a deck "yes", not a watchlist add (A2, decided). Saving to the watchlist is a
  separate button on the card (`PUT`/`DELETE /api/library/{tmdbId}/watchlist`, §7).
- The top 3 is chosen from the deck's right swipes only. Our own order (genre fit with the mood/swipes, then TMDB
  popularity) comes first; the AI then ranks the best of them from Wikidata facts and our own data and adds a reason
  (BE-13, as built below). No reason is ever an error: `reason` is simply `null`.
- Mood chips map to our genres/filters on the server; free `text` goes through `mood-parse.v2`. `language` and
  `maxRuntime` are plain filters, not LLM input.
- **Unclear, off-topic or injected text (ADR-0022):** empty text makes no LLM call. Text the model labels `unclear` or
  `offTopic` → with chips: the deck is built from the chips and `mood.textIgnored = true`; without chips: 422
  `mood.unclear`, nothing created. No response or log ever repeats the user's text.
- **The AI can't read the text (BE-19, owner 2026-11-16):** provider down or no answer within 15 s, refusal, invalid reply,
  or the daily AI budget spent → with chips: the deck is built from the chips, `textIgnored = true` and
  `textUnavailable = true`; without chips: 422 `mood.text_unavailable`, nothing created. Never an `ai.*` code or a 5xx.
- Solo decks require a signed-in user; no anonymous "Just swipe" (A3, decided).
- Routes (A4, decided): `/mood` (picker), `/deck/:deckId` (swipe), `/deck/:deckId/top` (top 3; it belongs to a deck,
  "from 14 swipes").
- As built (BE-08, `justSwipe`):
  - Batches of 10 cards (`Deck.BatchSize`), positions from 1; a deck stops at 300 cards (`Deck.MaxItems`), then
    `POST …/items` returns `[]`. Also `[]` once no eligible film is left.
  - Eligible: available films (not A14) not yet in this deck and not `seen` in the caller's library at dealing time. A
    film is never twice in one deck (unique in the database). Candidates are the 500 most popular eligible films.
  - The first batch is the **sampler**: one film per main genre in turn, decades in turn within a genre, most popular
    first (±10% noise, so two decks differ). Later batches: 70% by fit with this deck's swipes and the caller's 200 most
    recent library rows (genres, runtime, embeddings of the same model when present, a little popularity), 30% still from
    the sampler. Weights: right +1, left −1, seen + loved +1.5, seen + not for me −1, seen alone +0.3; library liked +1,
    watchlist +0.5, high rating +1, low rating −1, all × 0.6.
  - `justSwipe` with a `mood` → 400 `errors.mood`. `seenVerdict` without `up` → 400 `errors.seenVerdict`.
  - Swipe retries: the same `swipeId` on the same card → 204, nothing changes; a `swipeId` already used for another card,
    or a new one for a swiped card → 409 `deck.already_swiped`.
  - Top 3: right swipes ordered by genre fit with the deck's swipes and the library, then TMDB popularity, then deck order.
- As built (BE-09, `mood`; the full flow is in docs/ai.md → Mood flow):
  - `source: "mood"` without `mood` → 400 `errors.mood`. Chips are de-duplicated; `language` is stored lowercase.
  - Chips map to genres by a server-side table; `maxRuntime` (`under90` → < 90 min, `upTo2h` → ≤ 120) and `language`
    (the film's original language) are filters, never LLM input. Empty or whitespace `text` makes no LLM call and comes
    back as `text: null`.
  - Each ADR-0022 row is a slice test. With a mood in the text, its filters join the chips'; a chip's genre is never
    excluded by the text, and the stricter runtime wins. Genres the model returns that our catalog doesn't have are dropped.
  - `mood.filters` is what every batch is limited to (a film missing the needed value doesn't pass); its genres also
    weigh in adaptation and the top 3. `keywords` are stored but don't filter yet (BE-13).
  - If no film passes the filters, the deck is created with `items: []` and `POST …/items` returns `[]`.
  - `mood.text` is the caller's own text in their own deck; no error or log ever contains it.

## 6. Rooms and votes (ADR-0012, ADR-0015, docs/realtime.md)

A host (signed-in user) creates a room; friends open the link (`/rooms/{code}`), see a preview, and join as guests or
users. Mood rooms have a mood step in the lobby. The host starts; everyone swipes the shared deck; the server decides
match or best compromise. Codes look like `POP-428` (3 letters, dash, 3 digits), case-insensitive.

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `POST /api/rooms` | user | `CreateRoomRequest` | 201 `RoomDto`, `Location: /api/rooms/{code}` | 400, 401 |
| `GET /api/rooms/{code}/preview` | anon | — | 200 `RoomPreviewDto` | 404 `room.not_found` |
| `POST /api/rooms/{code}/join` | anon | `JoinRoomRequest` | 200 `RoomDto` (+ guest cookie when anonymous) | 400, 404, 409 `room.closed` / `room.full`, 429 `rate.limited` |
| `GET /api/rooms/{code}` | member | — | 200 `RoomDto` | 401, 403 `room.not_member`, 404 |
| `PUT /api/rooms/{code}/mood` | member | `SetRoomMoodRequest` | 200 `RoomMoodDto` (now ready) | 400, 401, 403, 404, 409 `room.invalid_state`, 422 `mood.unclear` / `mood.text_unavailable`, 429 `rate.limited` |
| `POST /api/rooms/{code}/start` | host | — | 200 `RoomDto` (`status: "swiping"`) | 401, 403 `room.not_host`, 404, 409 `room.invalid_state` |
| `GET /api/rooms/{code}/deck?fromPosition=0` | member | — | 200 `DeckItemDto[]` (`swipe` = my vote) | 401, 403, 404, 409 `room.invalid_state` (lobby) |
| `POST /api/rooms/{code}/votes` | member | `CastVoteRequest` | 204 | 400, 401, 403, 404 / `deck.item_not_found`, 409 `room.already_voted` / `room.invalid_state` / `room.closed` |
| `GET /api/rooms/{code}/progress` | member | — | 200 `RoomProgressDto` | 401, 403, 404 |
| `POST /api/rooms/{code}/finish` | host | — | 200 `RoomResultDto` (compromise if no match yet) | 401, 403 `room.not_host`, 404, 409 `room.invalid_state` |
| `GET /api/rooms/{code}/result` | member | — | 200 `RoomResultDto` | 401, 403, 404, 409 `room.no_result`, 429 `rate.limited` |
| `POST /api/rooms/{code}/leave` | member | — | 204 | 401, 403, 404 |
| `POST /api/rooms/{code}/close` | host | — | 204 | 401, 403 `room.not_host`, 404 |

```ts
type RoomMode   = "justSwipe" | "mood" | "taste"   // "taste" (group taste): §12 (BE-14a), built by BE-14
type RoomStatus = "lobby" | "swiping" | "result" | "closed"
type CreateRoomRequest = { name: string; mode: RoomMode; startsAt: string | null }   // name 1–40 ("Friday Film Club")
type JoinRoomRequest   = { displayName: string | null }   // required (1–30) when anonymous; ignored for users
type SetRoomMoodRequest = { moods: Mood[]; maxRuntime: RuntimeLimit; hardNos: HardNo[]; text: string | null }
type RoomMoodDto = { member: RoomMemberDto; textIgnored: boolean; textUnavailable: boolean }   // only ever sent to that member (ADR-0022); textUnavailable: + BE-19, as in §5
type CastVoteRequest   = { voteId: string; deckItemId: string; direction: SwipeDirection; seenVerdict: SeenVerdict | null }

type RoomMemberDto = { id: string; displayName: string; kind: "user" | "guest"; isHost: boolean; isReady: boolean; joinedAt: string }
type RoomDto = {
  id: string; code: string; name: string; mode: RoomMode; status: RoomStatus; startsAt: string | null; createdAt: string;
  deckSize: number;                 // "12 films to swipe"; grows when the deck is extended
  maxMembers: number;               // 8 ("ADMIT 2–8")
  me: RoomMemberDto; members: RoomMemberDto[];
}
type RoomPreviewDto = { code: string; name: string; hostDisplayName: string; mode: RoomMode; status: RoomStatus; memberCount: number; maxMembers: number }
type RoomProgressDto = { status: RoomStatus; deckSize: number; members: { memberId: string; votedCount: number }[] }
type RoomResultDto = {
  kind: "match" | "compromise";
  picks: RoomPickDto[];             // match: exactly 1; compromise: up to 3, best first
  memberCount: number; decidedAt: string;
}
type RoomPickDto = {
  rank: number; film: FilmSummaryDto;
  yesCount: number; noCount: number;
  yes: string[];    // memberIds who swiped right
  seen: string[];   // memberIds who swiped up ("Omar passed — he's seen it")
  reason: string | null;   // the AI's one-liner about this pick (BE-13), null when none; never names who said no
}                   // who said no is never revealed, only how many (A7)
```
Rules (all in services; see docs/realtime.md for when each event is published):
- **Join** is idempotent (already a member → 200). Late arrivals may join while `swiping` ("Late arrivals can still hop in").
  Joining publishes `memberJoined`; leave publishes `memberLeft`; close publishes `roomClosed`.
  `PUT …/mood` publishes `memberReady`; `POST …/start` publishes `roomStarted` (both land with BE-11).
- **Ready:** in `mood` rooms a member is ready after `PUT …/mood`; in `justSwipe` rooms every member is ready. The host
  may start with members not ready ("2 OF 3 READY").
- **Hard nos** are private to the member ("the group never sees these") and a film matching any member's hard no never
  enters the shared deck nor the result (glossary: Hard no).
- **Votes** follow ADR-0015: unique per (`DeckItem`, `RoomMember`), `voteId` makes retries idempotent, `voteCast` carries
  no values. Votes can't be changed. `seenVerdict` on a vote doesn't touch the member's library. ⚠ Open (A5): confirm.
- **Match:** every current member voted `right` on the same item → `status: "result"`, `kind: "match"`, `roomMatched`.
- **Best compromise:** when every member has voted on every item without a match, or the host calls `finish`: rank by
  `right` votes (ties: fewer `left`, then TMDB popularity) → `kind: "compromise"`, `roomMatched` with the #1 film.
- **Deck extension** ("Just swipe together", "Swipe 6 more"): the server adds a batch when members near the end and
  publishes `deckExtended`. ⚠ Open (A6): the "Swipe 6 more" and "Tiebreak" buttons on the close-call board are not in
  the first wave.
- **Result visibility (A7, decided):** for the picked films only, the result names who said yes
  and who has seen it, and gives just the **number** of noes, never who said no. Hard nos and mood answers are never
  shown. Nobody's "no" can be singled out, while the reason a film lost ("one of you has seen it") stays visible.
- **Hub:** `JoinRoom(code)` / `LeaveRoom(code)` on `/hubs/rooms`, membership checked by a service (ADR-0012); clients
  then refetch through the endpoints above. Events: `contracts/realtime-events.json`.

**As built (BE-10):** create, preview, join, get, leave and close are live (finish and result: BE-12 below).
- **Codes** are drawn at random (`AAA-100`…`ZZZ-999`), unique, stored upper case; any case and surrounding spaces are
  accepted. A malformed code is `room.not_found`. Closed rooms keep their code and stay previewable (`status: "closed"`).
- **Check order** for member and host endpoints: `room.not_found` (404), then `auth.unauthenticated` (401), then
  `room.not_member` / `room.not_host` (403). A guest is a member only of the room their cookie names; their cookie gets
  403 `room.not_member` anywhere else.
- **Host** (owner's rule): the member present longest. The creator hosts while in the room; when the host leaves, hosting
  passes to the next longest-present member, guest or user. The last member to leave closes the room (`roomClosed`;
  known gap: two last members leaving at the same moment can leave it open, issue #39).
- **Join:** a present member gets 200 and no event (idempotent, even once closed). Otherwise `room.closed` once the
  room is `result` or `closed`, then `room.full` with 8 present members. A user joins under their account name
  (`displayName` ignored) and, after leaving, comes back as the same member (no longer host). Anyone else, including a
  guest of another room (A1), needs `displayName` (400 `validation` on `displayName`) and becomes a new guest with a
  session for this room only. Join answers with a fresh `XSRF-TOKEN`.
- **Leave** (owner's rule): a guest's session ends with it (cookie cleared, fresh `XSRF-TOKEN`; `GET /api/auth/me` →
  401); coming back means joining as a new guest. A user stays signed in.
- **Close** is host-only and idempotent (204 again); members still `GET` the room (`status: "closed"`), nobody joins.
- **Guest upgrade:** `register` or `sign-in` with a guest cookie moves that guest's membership (same member id, so its
  votes follow) to the user. If the user is already present in that room, the user's membership stays and the guest's ends.
- `deckSize` is 0 until the deck is dealt at start (BE-11); `isReady` is true for everyone in `justSwipe` rooms and, in
  `mood` rooms, true once the member answered the mood step (BE-11).
- **Hub:** `JoinRoom(code)` fails with a `HubException` whose message is the error code (`room.not_found`,
  `auth.unauthenticated`, `room.not_member`). The connection's identity is the one it connected with: a client that just
  joined (or upgraded) over HTTP reconnects before `JoinRoom`. `LeaveRoom(code)` only stops that connection's events.

**As built (BE-11):** mood step, start, deck, votes and progress are live (owner's decisions of 2026-11-14 below).
- **Mood step** (`PUT …/mood`): `mood` rooms in the `lobby` only, else 409 `room.invalid_state`. Same rules as a solo mood
  (ADR-0022: `mood.unclear` without chips, `textIgnored` with chips; when the AI can't read the text, BE-19:
  `mood.text_unavailable` without chips, `textIgnored` + `textUnavailable` with chips; nothing changes on an error).
  A second answer replaces the first. `memberReady` only when the member first becomes ready. Hard nos are stored on the
  membership and appear in no response.
- **Hard nos** (owner: "wide nets"; `HardNoRules`): `horror` = TMDB Horror; `gore` = TMDB Horror or a Wikidata genre
  containing splatter / slasher / body horror / zombie / gore; `musicals` = TMDB Music or Wikidata "musical";
  `sadEndings` = TMDB War or Wikidata "tragedy"; `subtitles` = a recorded original language other than `en` (films with
  none pass). Every present member's hard nos apply to every batch.
- **Start** (host, `lobby` only; a second or concurrent start is 409 `room.invalid_state`): one shared deck per room,
  first batch of 10. Moods combine (owner: "union, strictest runtime"): any genre a ready member asked for, minus genres a
  member ruled out unless another asked for them; the shortest runtime limit; years/language only when every mood set
  one. Members who skipped the mood step don't count; no moods → the sampler. When the moods leave too few films the
  batch is topped up by the sampler, hard nos still excluded. Members need not be ready.
- **Deck** (`GET …/deck`): positions `>= fromPosition` (0 = all); 409 `room.invalid_state` in the `lobby`, readable
  after. `swipe` is the caller's vote; `onWatchlist` is the caller's (always false for guests).
- **Votes:** check order after membership: 400 `seenVerdict` (a verdict only with `up`), then a known `voteId` (same
  member and card → 204 even after swiping ended; otherwise 409 `room.already_voted`), then 409 `room.closed` /
  `room.invalid_state` unless `swiping`, then 404 `deck.item_not_found`, then 409 `room.already_voted`. A vote never
  touches the library (A5: no).
- **Extension** (owner: cap 30): when a voter has 3 or fewer cards left, the next batch of 10 is dealt (same filters),
  up to 30 cards; `deckExtended` carries the first new position. A room deck at 30 doesn't grow; ending a room is BE-12.
- **Progress:** present members, host first, with how many cards each voted on; `deckSize` 0 in the lobby.
- **Races:** rooms carry an optimistic concurrency token (Postgres `xmin`), so a start racing a close or the last member
  leaving can't reopen the room: the close wins and the start answers 409 `room.invalid_state`. Taste rooms: §12 (BE-14).
- **Mood retention** (owner, issue #51): a room mood keeps no free text (only chips, runtime and the filters derived
  from it, plus `textIgnored`) and belongs to its member: deleting the member, guest or room deletes it.

**As built (BE-12):** finish and result are live (owner's decisions of 2026-11-15 below; rules in `RoomResultRules`).
- **Who counts:** only present members. A match needs every present member's `right` on one card (several at once: the
  earliest in the deck). Votes of members who left count nowhere; `memberCount` and each pick's `yes` / `seen` /
  `noCount` are frozen at the members present when the room was decided, so the result doesn't change when people leave.
- **When:** after every vote (match, or every present member voted on every card: compromise) and after a member leaves
  (the rest may now agree or be done), each checked after that request's own commit so simultaneous last votes can't miss
  the match; the room's concurrency token makes the result unique (exactly one `roomMatched`; ADR-0026). The result
  counts only the votes the decision saw. A retried vote (known `voteId`) checks again. The room becomes
  `result`; votes then answer 409 `room.invalid_state` (a retried `voteId` still 204).
- **Compromise:** only cards at least one present member swiped right on, ranked by `right`, then fewer `left`, then
  TMDB popularity (unknown last), then deck position; the top 3. `up` (seen it) is neither yes nor no. When nobody said
  yes to anything, `picks` is empty and `roomMatched` carries `tmdbId: null`.
- **Finish** (host): while `swiping` decides now (the match if there is one, else the compromise); on a decided room it
  returns the same result again (200); in the `lobby` or once `closed` 409 `room.invalid_state`.
- **Result** (member): the stored result, also after the room was closed; 409 `room.no_result` before it is decided.
- `yes` lists members in the order they voted. Hard nos can't reach the result because they never reach the deck (they
  are set in the lobby, before the deck is dealt), so the result doesn't filter them again.

**As built (BE-13):** AI reasons on picks (owner's decisions of 2026-11-16; docs/ai.md → AI picks).
- **Solo top 3:** our own order of the right swipes as before; the best 20 that have Wikidata facts go to
  `rank-candidates.v2` with the user's own signals (the deck's mood genres, keywords and runtime; Wikidata genres of films
  they loved or swiped away). Its top 3 come first with their reasons; ids we didn't send and repeats are dropped; the
  rest fill up in our order with `reason: null`. Ranked on every `GET …/top` (nothing stored), so a new swipe can change it.
- **Room result:** the picks and their order stay exactly as §6 decides (votes, then popularity); the AI only adds
  `reason` (`explain-pick.v2`, given each pick's Wikidata facts and its yes / no / seen **counts**, never who voted what,
  anyone's mood or hard nos). Written on the first read of `GET …/result` and stored on the room, so every member sees
  the same reasons; `finish` returns the stored reasons, or `null` before that first read; picks without facts get `null`.
- **Failures:** provider down, refusal, invalid reply or a 20 s timeout → the same picks with `reason: null` and a 200,
  never an error. A room then stores nothing, so a later read tries again.

## 7. Library — My Cinema (UserFilm, ADR-0019)

One `UserFilm` per user and film, with independent flags (the Details A board toggles Like, Saved, Seen it) and a rating
of 0.5–5 in steps of 0.5, shown as ticket stubs (half stars, ADR-0024; until BE-20 lands the API takes whole 1–5). There is no "Watching" state (A9, decided: dropped).

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `GET /api/library?shelf=liked&sort=recent&genre=&page&pageSize` | user | — | 200 `PagedResult<UserFilmDto>` | 400, 401 |
| `GET /api/library/summary` | user | — | 200 `LibrarySummaryDto` | 401 |
| `GET /api/library/{tmdbId}` | user | — | 200 `UserFilmDto` (all false/null when untouched) | 401, 404 `film.not_found` |
| `PUT /api/library/{tmdbId}` | user | `UpdateUserFilmRequest` | 200 `UserFilmDto` | 400, 401, 404 `film.not_found` |
| `PUT /api/library/{tmdbId}/watchlist` | user | — | 204 (idempotent add; other flags untouched) | 401, 404 `film.not_found` |
| `DELETE /api/library/{tmdbId}/watchlist` | user | — | 204 (idempotent remove) | 401, 404 `film.not_found` |

```ts
type LibraryShelf = "liked" | "watchlist" | "seen"
type LibrarySort  = "recent" | "title" | "rating"
type UpdateUserFilmRequest = { liked: boolean; onWatchlist: boolean; seen: boolean; rating: number | null }  // rating 0.5–5, steps of 0.5
type UserFilmDto = UpdateUserFilmRequest & { film: FilmSummaryDto; updatedAt: string }
type LibrarySummaryDto = { liked: number; watchlist: number; seen: number }
```
- `PUT` replaces the whole state (last write wins); all false + `rating: null` deletes the row. The `/watchlist`
  pair changes only that flag, for the deck card's watchlist button, where the client doesn't hold the full state.
- A rating implies `seen = true` (server rule; A8, decided). `liked` stays a separate flag; the Liked shelf shows the
  rating when there is one (e.g. "3.5/5").
- **Half stars (ADR-0024, BE-20):** `rating` is 0.5, 1, 1.5 … 5 or `null`. Anything else (0, 5.5, 3.25, 7) → 400
  `validation` with `errors.rating`. Stored as an integer count of half stars (1–10); the API never shows that number.
  `sort=rating` is unchanged (highest first, unrated last). Imports write ratings through the same rules (§13).
  As built (BE-20): `rating` is a JSON number (`decimal?`; OpenAPI `number`); whole numbers stay valid and come back as
  whole numbers (`4`, not `4.0`). Stored as `user_films.rating_half_stars smallint` (check 1–10); the migration doubled
  existing ratings. `UserFilm.IsValidRating` is the one rule (request attribute `[HalfStarRating]` and the domain guard).
- ADR-0019 lands here: referenced films are refreshed by the sync, never purged; FKs to `films` are `ON DELETE RESTRICT`.
- As built (BE-05): `shelf` defaults to `liked`, `sort` to `recent` (last change first); `title` is A→Z; `rating` is
  highest first, unrated last; every order ends on `tmdbId`. `genre` matches one of the film's genres exactly. An
  untouched film's `updatedAt` is the current time (the field isn't nullable). The `/watchlist` pair removing the last
  flag deletes the row, like `PUT`.
- A14 (as built): a film TMDB no longer knows keeps its row with `overview`/`posterPath` cleared and is flagged
  unavailable server-side (`films.is_available`); no screen needs the flag yet, so `FilmDto` doesn't carry it.

## 8. Film details

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `GET /api/films/{tmdbId}` | anon | — | 200 `FilmDto` | 404 `film.not_found` — exists |
| `GET /api/films/{tmdbId}/trailer` | anon | — | 200 `FilmTrailerDto` | 404 `film.not_found`, 503 `tmdb.unavailable` |

```ts
type FilmTrailerDto = { trailer: { site: "YouTube"; key: string; name: string } | null }
```
- The trailer comes from TMDB on first request and is cached (ADR-0003), at most 180 days (ADR-0017). Never on the swipe path.
- As built (BE-07): the first trailer TMDB lists on YouTube (`site` "YouTube", case-insensitive), `null` when there is none;
  both answers are cached per film (`film_trailers`). A cached answer is served without calling TMDB for 30 days, then
  refreshed; if TMDB fails, a cached answer up to 180 days old is served, and `503 tmdb.unavailable` only when there is
  none. The nightly sync deletes cached trailers older than 180 days. A film TMDB no longer knows (A14) answers
  `{ trailer: null }` without calling TMDB.
- The caller's state on the details page comes from `GET /api/library/{tmdbId}` (signed in only).
- Out of scope: the personal "WHY" note (needs taste + Wikidata: P-03), certification ("PG-13"), and where to watch
  ("Watch on Mubi / Rent on Apple TV / Open in Letterboxd": proposed task P-01, needs the owner's approval).

## 9. Browse

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `GET /api/films?search=&genre=&maxRuntime=&minVoteAverage=&hideSeen=&sort=popular&page&pageSize` | anon | — | 200 `PagedResult<FilmSummaryDto>` | 400 |
| `GET /api/films/genres` | anon | — | 200 `string[]` (genres present in our catalog, alphabetical) | — |

- `search`: case-insensitive title substring, ≤ 100 chars. `maxRuntime`: `RuntimeLimit`. `minVoteAverage`: 0–10.
  `sort`: `popular` (TMDB popularity at last sync — "Trending this week") | `newest` | `rating`.
- `hideSeen` needs a user; for anonymous callers it is ignored.
- Out of scope: "On my services" and "New on Netflix, Mubi & Prime" (where-to-watch data, P-01).
- As built (BE-06): every filter that is set narrows the result (they combine). `search` and `genre` are trimmed; `genre`
  is an exact match against the values of `GET /api/films/genres`. `maxRuntime`: `under90` = under 90 min, `upTo2h` = at
  most 120 min, `any` = no filter; a film with no runtime never passes `under90`/`upTo2h`. `minVoteAverage` keeps films
  at or above it (films without a score never pass). Every sort puts films missing the sorted value last and ends on
  TMDB id, so pages are stable. Films TMDB no longer knows (`is_available = false`, A14) are not listed.

## 10. Changing this contract

Mirrors FlowOps (ADR-0021):
1. **Backend owns the contract.** A change the frontend waits for is its own merged task (contract + 501 stubs, e.g.
   BE-14a); otherwise it may ride in the backend task that needs it. Either way: update this file, the
   DTOs/controllers, `./scripts/export-openapi.sh`, `cd src/frontend && npm run api:types`, and list it under
   **Contract change** in the PR. CI fails if `contracts/openapi.json` or `schema.d.ts` drift from the code.
2. **The frontend agent never edits** this file, `contracts/` or `schema.d.ts` by hand. Missing field or endpoint → open an
   issue (`contract` label, AGENTS.md §4) and mock nothing that isn't in the contract.
3. **Breaking changes** (rename, remove, retype, new required input) go additive first: add, migrate consumers, remove in a
   later task. Adding an enum value (e.g. `taste`) is additive but must be handled by the frontend in the same release.
4. A stub (`501 server.not_implemented`) is part of the contract: its shapes are final, only the behaviour is missing.
   BE-17 removed the stub plumbing once nothing was stubbed (§2); a task that stubs again re-adds it.

### As built (BE-01)
The stubs follow this file exactly. Names the shapes above leave implicit, now fixed in `contracts/openapi.json`:
- `"user" | "guest"` (`CurrentUserDto.kind`, `RoomMemberDto.kind`) is the enum `PrincipalKind`; `RoomResultDto.kind` is
  `RoomResultKind`; `RoomProgressDto.members[]` is `MemberProgressDto`; `FilmTrailerDto.trailer` is `TrailerDto` with
  `site: TrailerSite` (`"YouTube"`, TMDB's spelling kept).
- `MoodInput` keeps its name (it is part of `CreateDeckRequest`, not a request of its own).
- Browse and library sorts are the enums `FilmSort` and `LibrarySort`; query parameters are camelCase like JSON.
- Stubbed actions also declared `501` (`[ContractStub]`); feature tasks removed the attribute, BE-17 the attribute itself.

## 11. Open questions

| Id | Question | Assumption in this contract |
|---|---|---|
| A1 | Guest in two rooms at once? | One room per guest identity; opening another room replaces it |
| A2 | Do solo right swipes add to the watchlist? | **Decided: no.** A separate watchlist button on the card (bottom or left; FE-05 picks the spot) |
| A3 | Can anonymous visitors use solo "Just swipe"? | **Decided: no.** Solo needs a signed-in user |
| A4 | Routes for deck and top 3 | **Decided:** `/deck/:deckId` and `/deck/:deckId/top`; `/mood` is the picker |
| A5 | Can a room vote be changed? Does `seenVerdict` in a room update the library? | No and no |
| A6 | "Swipe 6 more" / "Tiebreak" on the close-call board | Not first wave |
| A7 | Reveal each member's votes on the result screen? | **Decided:** name yes and seen, count noes only; picks only |
| A8 | Rating implies seen? Liked vs rating? | **Decided:** rating ⇒ seen; liked is a separate flag |
| A9 | "Watching" shelf and "Keep watching · 42 min left" | **Decided: dropped.** No Watching state, shelf or Tonight row |
| A10 | Room codes `POP-428` vs the board's share link `ithaca.app/r/pop-428` | Links use the existing route `/rooms/POP-428`; a `/r/:code` short link is optional later |
| A11 | Group taste ("Blend your histories") with guests who have no history | ⚠ Assumed in §12 (BE-14a), built so by BE-14: the host must be ready; members without history add no taste; films any present user has seen are skipped (guests have no library, so they add none) |
| A12 | The glossary has no names for a solo deck's parent record or a solo swipe (`Vote` is a room member's) | **Proposed by BE-08:** `Deck` (table `decks`) and `Swipe` (table `swipes`), in the glossary; owner to confirm |
| A13 | Room mood has no `language` (solo mood does); is it stored as a `MoodQuery`? | No language in rooms; stored per member as a `MoodQuery` |
| A14 | ADR-0019's "unavailable" films (TMDB 404) have no flag in `FilmDto` | Add `isAvailable` additively in BE-05 if screens need it |
| A15 | Can guests import (ADR-0024 Q1)? Guests have no library (ADR-0012) | **Decided: no.** `/api/imports` is user-only; `/import` sends guests to sign in or create an account |
| A16 | How much history makes By taste "ready" without an import (ADR-0024 Q2)? | **Decided:** ≥ 10 films liked, rated ≥ 3.5 or "loved it"; the server decides and §12 (BE-14a) exposes it |
| A17 | Does a re-import update a rating that came from an earlier import (ADR-0024 Q5)? | **Decided: no.** Any existing rating wins, imported or not; ratings carry no source |
| A18 | A single uploaded CSV has the same columns whether it is watched, watchlist or likes | Identified by its export file name; anything else is `import.invalid_file` |

## 12. Taste (BE-14a)

"By taste" (solo) and group taste build on the caller's `TasteProfile`: a vector in the film-embedding space made from
the films they like (BE-14, docs/ai.md). Imported likes and ratings count like any other (ADR-0024 decision 9). The
server decides readiness; the "Solo — by taste" screen only shows it (FE-13: not ready → the "Import your Letterboxd"
screen; ready → the board).

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `GET /api/taste` | user | — | 200 `TasteDto` | 401 |
| `POST /api/decks` with `source: "taste"` (§5) | user | `CreateDeckRequest`, `mood: null` | 201 `DeckDto` | 400, 401, 422 `taste.not_ready` |
| `POST /api/rooms` with `mode: "taste"` (§6) | user (the host) | `CreateRoomRequest` | 201 `RoomDto` | 400, 401, 422 `taste.not_ready` |

```ts
type TasteDto = {
  ready: boolean;              // signalCount >= signalsNeeded
  signalCount: number;         // films in My Cinema liked, rated >= 3.5, or "loved it" (A16)
  signalsNeeded: number;       // 10: a config value BE-14 may tune; the screen shows "3 of 10"-style progress from it
  loggedCount: number;         // films marked seen or rated ("From 214 films you've logged")
  topGenres: TasteGenreDto[];  // the taste receipt: up to 3, biggest share first; [] when not ready
  notForYou: string[];         // up to 3 genres turned down ("NOT FOR YOU: HORROR, HEISTS"); [] when not ready
}
type TasteGenreDto = { genre: string; percent: number }   // a catalog genre (as in FilmDto.genres); 1–100
```
Rules (BE-14 built them; until then the three rows answered 501, a code BE-17 removed, §2):
- **Ready** (A16, ADR-0024 Q2): at least `signalsNeeded` films with a positive signal. Guests have no taste (no
  library, ADR-0012): `GET /api/taste` is user-only.
- **Receipt:** `percent` is a genre's share among the positive-signal films' genres, rounded; `notForYou` comes from
  disliked or low-rated films and left swipes. Genres are display data (catalog genres), never sent to AI as TMDB text
  (ADR-0017).
- **Deck by taste:** like "Just swipe" (§5: batches, swipes, top 3) but ranked by similarity to the profile; skips films
  the user has seen. Not ready → 422 `taste.not_ready`, nothing created. `mood` must be null (400 `errors.mood`).
- **Group taste** ("Blend your histories"): the host must be ready (else 422 `taste.not_ready`, no room). The shared
  deck blends present members' profiles; members without history (guests included) add no taste, and a film any member
  has seen is skipped (A11, ⚠ assumed). Readiness in the lobby works as in `justSwipe` rooms (everyone is ready).
- Check order once built: input validation (400: `mood` with a taste deck, a blank room name) before readiness (422).
- The board's "Because you loved …" lines under the first cards are not in this contract (no field yet); the first
  cards are simply the deck's first items.

**As built (BE-14):** the three rows are live (no 501 left in §12).
- **Signals and counts:** `signalCount` = library rows liked (a seen + "loved it" swipe sets liked) or rated ≥ 3.5;
  `loggedCount` = rows seen or rated. `signalsNeeded` is `Taste:SignalsNeeded` (default 10).
- **`TasteProfile`** (computed per request, not stored): the user's 200 most recently changed liked or rated films with
  a vector, weighted on the half-star scale (ADR-0024): liked +1, plus (rating − 3) / 2 clamped to ±1 (5 → +1, 3 → 0,
  1 → −1); the weighted sum of their film vectors (one model: the one most of them share), scaled to unit length. Only
  film vectors are used (Wikidata-based, ADR-0017); no text is embedded, so taste makes no AI call.
- **Receipt:** `topGenres` share = a genre's mentions among the positive-signal films' genres ÷ all their genre mentions
  (a film with two genres counts once for each), rounded, 1–100, biggest first then by name. `notForYou`: genres in at least
  2 turned-down films (rated ≤ 2 and not liked, or a solo left swipe or "seen — not for me", latest 500 swipes) whose share
  of the turned-down films is larger than of the positive ones, never a top genre; biggest difference first.
- **Deck by taste:** as "Just swipe" (§5 as built: batches of 10, the 500 most popular eligible films, 70% fit / 30%
  sampler, seen films skipped, swipes and top 3 unchanged) except that every batch, the first included, adds each
  candidate's closeness to the profile (cosine, scaled 0–1 across the candidates; ×1.5) to its fit. Films without a vector
  of the profile's model get no closeness. A ready user whose films have no vectors yet gets a plain "Just swipe" order.
- **Group taste:** everyone is ready in the lobby (as in `justSwipe` rooms). At start and at each extension the deck leans
  towards the blend of the present users' profiles (each member counts the same; members without a profile, guests
  included, add nothing) and skips every film a present user has seen. Hard nos still apply (there is no lobby step to
  set them in a taste room). The host's readiness is checked only at creation.

## 13. Import (Letterboxd)

Design of ADR-0024 (written by OPS-12; BE-15a adds the DTOs, codes and 501 stubs, BE-15 the logic). The user uploads
their Letterboxd export; a background `ImportJob` merges it into their library and reports what it couldn't match.

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `POST /api/imports` | user | `multipart/form-data`: `CreateImportRequest` | 201 `ImportJobDto` (`queued`), `Location: /api/imports/{id}` | 400 `validation` (no file) / `import.invalid_file`, 401, 409 `import.already_running`, 413 `request.too_large` / `import.file_too_large`, 422 `import.nothing_to_import` / `import.too_many_films`, 429 `rate.limited` (once BE-19 adds it) |
| `GET /api/imports?page&pageSize` | user | — | 200 `PagedResult<ImportJobDto>`, newest first | 400, 401 |
| `GET /api/imports/{id}` | user | — | 200 `ImportJobDto` | 401, 404 `import.not_found` |
| `GET /api/imports/{id}/not-found?page&pageSize` | user | — | 200 `PagedResult<ImportNotFoundDto>` | 400, 401, 404 `import.not_found` |

```ts
type CreateImportRequest = { file: Blob }   // form field "file": the Letterboxd ZIP, or one CSV from it
type ImportStatus = "queued" | "running" | "completed" | "failed"
type ImportJobDto = {
  id: string; status: ImportStatus; createdAt: string; finishedAt: string | null;
  filmCount: number;            // distinct films in the file (after merging the four files)
  processedCount: number;       // 0…filmCount: progress
  matchedCount: number;         // found in our catalog or on TMDB
  addedToCatalogCount: number;  // of matchedCount: films the job added from TMDB
  notFoundCount: number;        // films not found, kept even after the list itself is emptied (job rule 6)
  notFoundListKept: boolean;    // false for older imports: their not-found list returns an empty page
  changes: ImportChangesDto;    // what it changed in My Cinema; grows while running
}
type ImportChangesDto = {
  newFilms: number;     // UserFilm rows created
  seen: number; rated: number; liked: number; watchlist: number;   // flags/ratings newly set
  ratingsKept: number;  // films whose Ithaca rating was kept over the file's (job rule 3)
}
type ImportNotFoundReason = "noMatch" | "lookupFailed"
type ImportNotFoundDto = { title: string; year: number | null; letterboxdUri: string | null; reason: ImportNotFoundReason }
```

**Upload** (`POST`, all checks before anything is stored; the file itself is never stored, ADR-0024):
- Type by content: a ZIP (local-file signature) or a UTF-8 CSV whose header row is a Letterboxd export header. Extension
  and `Content-Type` are ignored. Anything else → 400 `import.invalid_file`.
- From a ZIP only `watched.csv`, `ratings.csv`, `watchlist.csv` and `likes/films.csv` are read; every other entry
  (`profile.csv`, `reviews.csv`, `diary.csv`, `comments.csv`, `lists/`, `deleted/`, nested archives…) is skipped
  unopened. None of the four present → 400 `import.invalid_file`. Columns used: `Name`, `Year`, `Letterboxd URI`, and
  `Rating` in `ratings.csv`; others are ignored; a missing `Name` column → `import.invalid_file`.
- A single CSV is identified by its file name (`watched.csv`, `ratings.csv`, `watchlist.csv`, or `films.csv` = likes);
  any other name → `import.invalid_file` (A18).
- Limits: upload ≤ **25 MB**, a constant (`ImportLimits.MaxUploadBytes`, since `[RequestSizeLimit]` needs one) → else 413
  `request.too_large` from the request pipeline. The rest in `ImportOptions`, these defaults: ZIP ≤ **2,000 entries** and compression
  ratio ≤ **100:1** per entry → else 400 `import.invalid_file`; the four files ≤ **50 MB** uncompressed in total, counted
  while reading (headers aren't trusted) → else 413; ≤ **20,000** distinct films → else 422 `import.too_many_films`.
- An empty `Year` is allowed (title-only matching, job rule 2). Rows with an empty `Name`, a present but non-numeric
  `Year` or a `Rating` outside 0.5–5 / not a half step are skipped and not
  counted; the file is still accepted. No usable rows at all → 422 `import.nothing_to_import`.
- One `queued` or `running` import per user → else 409 `import.already_running`. Creating imports is rate-limited per
  user (BE-19).
- Success: 201 with `status: "queued"`, `filmCount` known, all other counts 0.

**The job** (background, ADR-0008; never on a request path, ADR-0003):
1. A film is identified by its Letterboxd URI, else by title + year; the four files are merged per film first.
2. **Matching:** a URI matched before (by any user) reuses its TMDB id. Otherwise our catalog by normalised title
   (case, accents, punctuation) and exact year; else TMDB `search/movie` with the year: the result whose normalised
   title or original title equals the row's and whose release year equals it (else ±1); several → the most popular;
   none → `noMatch`; a row without a year matches only when exactly one result has the same normalised title. A
   URI → TMDB-id match is stored as a `LetterboxdFilmLink` (ids only). A found film not in our catalog is added by the
   same add-a-film step the catalog sync uses. TMDB failures are
   retried with back-off; still failing after the job's retry budget → `lookupFailed` and the job goes on.
3. **Merge, per matched film (ADR-0024: never remove, Ithaca wins):** `seen` ← existing or watched or rated; `liked` ←
   existing or liked; `onWatchlist` ← existing or on the watchlist; `rating` ← the file's rating **only if** the
   `UserFilm` has none (else counted in `ratingsKept`). Nothing is ever set to false and no row is deleted. A rating
   implies seen (A8). Changed rows get `updatedAt` = the time they were written; untouched rows keep theirs.
4. **Re-import:** the same rules on the existing rows; no duplicates (one `UserFilm` per user and film). A rating
   imported earlier counts as existing (A17).
5. Progress (`processedCount`, counts) is saved in batches; a job interrupted by a restart resumes. `completed` when
   every film is processed (even with not-found rows); `failed` only on an unexpected error, with nothing already
   written rolled back (a re-import is safe). No real-time event: the client polls `GET /api/imports/{id}` while the
   job is `queued` or `running`.
6. **Not-found list:** kept for the user's **latest** import only (an older import's list is emptied, its counts
   stay, `notFoundListKept: false`, and its not-found endpoint returns 200 with an empty page); sorted by title, then
   year. Deleting the account deletes imports and lists (FK cascade; §14).
7. Imported likes and ratings are plain `UserFilm` data and feed the `TasteProfile` like any other (§12, ADR-0017).
   Titles and rows are never logged.

**Who:** users only. A guest gets 401 `auth.unauthenticated` like every user-only endpoint (ADR-0023); `/import` sends
guests and visitors to sign in or create an account first (A15).

**Upload types (layering, AGENTS.md §2):** `CreateImportRequest` is the one request type that lives in the Api
(next to `ImportsController` in `Ithaca.Api/Controllers/`), because it carries an `IFormFile`, an ASP.NET Core type Application can't reference. The
action converts it with one Api helper (`request.ToImportUpload()`, no other logic) and calls
`ImportService.CreateAsync(ImportUpload upload, …)`; `ImportUpload(Stream Content, string FileName, long Length)` lives
in `Application/Imports`.

**As built (BE-15a):** all four actions exist with the shapes above; guests and visitors get 401 `auth.unauthenticated`. The upload is `multipart/form-data` with the field `file`
(binary); a request without it is 400 `validation` with `errors.file` (the generated schema doesn't mark the field
required, so the TypeScript type shows it optional: always send it). The upload is antiforgery-checked like every
unsafe request (`X-XSRF-TOKEN`). `[RequestSizeLimit(ImportLimits.MaxUploadBytes)]` (25 MB) makes the server refuse a
larger body; `[ReadFormFirst]` reads the form before model binding (which would otherwise report it as a 400) so
`ProblemDetailsExceptionHandler` answers 413 `request.too_large` (tested on real Kestrel). A client that sends the whole
body before reading the answer may see the connection close instead; browsers read the early 413. List queries use the shared
paging (`page` 1–10 000, `pageSize` 1–100). `ImportStatus` and `ImportNotFoundReason` are in `Domain/Imports`.

**As built (BE-15):** the rules above, with these details the design left open:
- **Upload:** the file is read in memory (`[RequestFormLimits]` keeps the whole form in memory, never a temp file) and
  dropped before the response; only the parsed films are stored (`import_items`). A recognised header has the columns
  `Name` and `Letterboxd URI` (plus `Rating` in `ratings.csv`); column names are exact, file names of a single CSV are
  matched case-insensitively, ZIP entries by exact path (a file inside an extra folder isn't read). The ratio limit is
  checked on the four files read, counting the bytes actually decompressed. Besides the skips listed above, a row is
  skipped when its name has a control character (a line break included) or is longer than a film title (300); a
  `ratings.csv` row with an empty `Rating` is skipped; a Letterboxd URI over 300 characters is dropped (the film is then
  identified by title + year). One active import per user is enforced by the partial unique index
  `ux_import_jobs_user_active`, so racing uploads get exactly one 201.
- **Matching:** the catalog step needs a year (a year-less row goes straight to TMDB). When the year-filtered TMDB
  search has no result within the year rule, a second search without the filter is made (a film TMDB files under the
  neighbouring year). URI → TMDB-id matches are kept in `letterboxd_film_links` (no foreign key: the film may be purged
  later and is then added again by id).
- **Counts:** `ratingsKept` counts films where the file's rating **differs** from the kept one (a re-import of the same
  ratings reports 0). A film added from TMDB counts in `addedToCatalogCount` once, even if two rows matched it.
- **Job:** `ImportJobRunner` polls every `Imports:PollInterval` (2 s) and processes batches of `Imports:BatchSize` (50)
  back to back, oldest import first. TMDB calls are paced (`TmdbRequestDelay`, 100 ms) and tried `TmdbAttempts` (3)
  times with doubling back-off from `TmdbRetryDelay` (1 s). An older import's not-found list is emptied when the newer
  job **starts** (not at upload). Imports run one at a time, oldest first across users (single instance, ADR-0016), so
  a large import delays the next. A write conflict (the user or the sync wrote the same row meanwhile) saves nothing and
  the batch is retried, up to `ImportJob.MaxConflictRetries` (5) times in a row, then the job fails; any other error fails the job: earlier batches stay, its pending items are dropped, its
  not-found rows so far stay as its list.

## 14. Profile (BE-16a)

The "Profile & settings" board (`/profile`, FE-15): the user's name, email and join date, editing the display name,
changing the password, and deleting the account. BE-16a adds the DTOs, the code and 501 stubs; BE-16 the logic
(`ProfileService` in `Application/Users`).

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `GET /api/profile` | user | — | 200 `ProfileDto` | 401 |
| `PUT /api/profile` | user | `UpdateProfileRequest` | 200 `ProfileDto` | 400, 401 |
| `PUT /api/profile/password` | user | `ChangePasswordRequest` | 204, new `XSRF-TOKEN` | 400 `validation` / `profile.wrong_password`, 401 |
| `DELETE /api/profile` | user | `DeleteAccountRequest` | 204, signed out, new `XSRF-TOKEN` | 400 `validation` / `profile.wrong_password`, 401 |

```ts
type ProfileDto           = { id: string; displayName: string; email: string; createdAt: string }  // "since Nov 2026"
type UpdateProfileRequest = { displayName: string }                           // trimmed, 1–40, as at registration
type ChangePasswordRequest = { currentPassword: string; newPassword: string } // new: 8–128, length only (ADR-0023)
type DeleteAccountRequest  = { password: string }                             // the current password
```

**Rules** (BE-16):
- **Who:** users only; a guest or visitor gets 401 `auth.unauthenticated` (ADR-0023). Guests have no profile.
- **Display name:** trimmed; blank after trimming → 400 `errors.displayName`. `GET /api/auth/me` returns the new name
  at once (the client refetches it). A user `RoomMember`'s name is the account's, so rooms show the new name too
  (owner, 2026-11-15: as BE-10 built it; a guest's name is the one they joined with).
- **Re-authentication (owner, 2026-11-14):** password change and deletion both need the current password, so a stolen
  session alone can't lock the owner out or erase the account. Wrong → 400 `profile.wrong_password`, nothing changed.
  A wrong password counts toward the sign-in lockout (5 failures, 15 minutes, §4), and while locked out the answer is
  the same `profile.wrong_password` (it never reveals the lockout). Check order: input validation (400 `validation`)
  first, then the password.
- **Password change:** the new password may equal the old one (length is the only rule). The security stamp changes:
  the caller's session stays signed in (re-issued cookie), every other session of that user ends. Use the new
  `XSRF-TOKEN` from the response.
- **Account deletion (owner, 2026-11-14)** removes, in one use case:
  - the account (Identity rows) and everything that belongs to it, by FK cascade from `users`: My Cinema (`UserFilm`),
    decks and their swipes, mood queries, the `TasteProfile`, and imports with their not-found lists (ADR-0024, §13);
  - the user's room memberships and their votes. Before that, every open room (`lobby` or `swiping`) the user hosts is
    closed (`roomClosed`, docs/realtime.md); in other open rooms they leave (`memberLeft`). Events are published after
    the commit. Finished rooms keep their result; that member's votes are gone from it.
  - **Not** removed: `Film`s (catalog, ADR-0019) and `LetterboxdFilmLink`s (ids only, shared by all users, §13).
  Then the caller is signed out (session cookie cleared, a new anonymous `XSRF-TOKEN` issued, as after sign-out). The
  email can register again at once.
- The board's other rows are not in this contract: taste chips come from `GET /api/taste` (§12); "Film history" from the
  latest import (`GET /api/imports?pageSize=1`, §13); "Sign out" is §4; streaming services (P-01), notifications
  (ADR-0014), appearance (a client setting), privacy and group history have no API yet.

**As built (BE-16):** all four actions are live. The security stamp is checked on every user request
(`SecurityStampValidatorOptions.ValidationInterval = 0`, AuthSetup, ADR-0027), so a password change ends the user's other sessions
at once and a deleted account's sessions answer 401 at once. A wrong password on password change or deletion counts toward
the sign-in lockout and is saved before anything else happens. Deletion closes the open rooms the user hosts and removes
the account in one commit (room memberships, votes and everything else by FK cascade), then signs out and publishes
`roomClosed` / `memberLeft`.

**As built (BE-16a):** the four actions existed with the shapes above and answered 501 (removed in BE-17, §2) until
BE-16; guests and visitors get 401 `auth.unauthenticated`. `DELETE /api/profile` takes a JSON body (`openapi-fetch`
sends it; keep the `Content-Type: application/json`). Password change and deletion re-issue `XSRF-TOKEN` like the §4 actions
(checked on the stub answer). A body missing a required property is a 400 `validation` on `$` (deserialisation),
an empty one a 400 on the field. `profile.wrong_password` is in all three code lists with its user text.
