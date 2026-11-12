# API contract (first wave)

**What this is:** the *design* contract between backend and frontend for the first-wave features: auth, solo deck and
swipes (incl. mood), rooms and votes, library (My Cinema), film details, browse. Once backlog task **BE-01** lands, the
*executable* contract is `contracts/openapi.json` (generated from the code) and the frontend types come from it
(`npm run api:types`). The two must agree; §10 says how to change either. Decision: ADR-0021 (contract-first).

Not repeated here: the error pipeline (docs/backend-conventions.md → Result and errors), real-time events
(`contracts/realtime-events.json` + docs/realtime.md), domain names (docs/glossary.md), AI rules (docs/ai.md).
Second-wave features (taste, import, profile, Tonight) get their contract sections in their own backlog task.

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
| 409 | `deck.already_swiped` | a different `swipeId` for an item already swiped |
| 409 | `room.already_voted` | a different `voteId` for an item this member already voted on |
| 409 | `room.closed` | the room is closed (or finished) and takes no joins, votes or changes |
| 409 | `room.full` | the room already has 8 members |
| 409 | `room.invalid_state` | action not allowed in the room's current `status` (e.g. vote in the lobby, start twice) |
| 409 | `room.no_result` | result requested before there is one |
| 422 | `deck.no_yeses` | top 3 requested before any right swipe |
| 422 | `mood.unclear` | mood text isn't a mood (unclear or off-topic) and no chips were tapped; nothing created (ADR-0022) |
| 422 / 502 / 503 | `ai.refused` / `ai.invalid_output` / `ai.unavailable` | mood parsing failed — exist (docs/ai.md) |
| 501 | `server.not_implemented` | contract stub: the endpoint exists, its backlog task hasn't landed (BE-01) |
| 503 | `tmdb.unavailable` | trailer lookup failed and nothing is cached — exists |

`server.not_implemented` is temporary plumbing: it disappears from an endpoint when its task lands, and the code itself
is deleted when no stub is left.

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
sign-in, sign-out, room join) re-issues it.

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `GET /api/auth/me` | anon | — | 200 `CurrentUserDto` (user or guest) | 401 `auth.unauthenticated` |
| `POST /api/auth/register` | anon | `RegisterRequest` | 201 `CurrentUserDto`, signed in | 400, 409 `auth.email_taken` |
| `POST /api/auth/sign-in` | anon | `SignInRequest` | 200 `CurrentUserDto` | 400, 401 `auth.invalid_credentials` |
| `POST /api/auth/sign-out` | user or guest | — | 204 (cookie cleared) | — |

```ts
type RegisterRequest = { email: string; password: string; displayName: string }  // password 8–128; name 1–40
type SignInRequest   = { email: string; password: string }
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

## 5. Solo deck and swipes (incl. by mood)

A solo deck belongs to one user. "Just swipe" starts with a varied **sampler** and adapts; "By mood" starts from a
`MoodQuery`. Cards are `DeckItem`s at increasing positions; the client asks for more as it nears the end.

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `POST /api/decks` | user | `CreateDeckRequest` | 201 `DeckDto` (first batch), `Location: /api/decks/{id}` | 400, 401, mood: 422 `mood.unclear` / `ai.refused`, 502 `ai.invalid_output`, 503 `ai.unavailable` |
| `GET /api/decks/{deckId}` | user | — | 200 `DeckDto` | 401, 404 `deck.not_found` |
| `POST /api/decks/{deckId}/items` | user | — | 200 `DeckItemDto[]` (next batch; `[]` = nothing left) | 401, 404 |
| `POST /api/decks/{deckId}/swipes` | user | `SwipeRequest` | 204 | 400, 401, 404 `deck.not_found` / `deck.item_not_found`, 409 `deck.already_swiped` |
| `GET /api/decks/{deckId}/top` | user | — | 200 `DeckTopDto` | 401, 404, 422 `deck.no_yeses` |

```ts
type DeckSource = "justSwipe" | "mood"          // "taste" is added by the taste task (additive)
type MoodInput  = {
  moods: Mood[];                 // 0–2 tapped chips
  maxRuntime: RuntimeLimit;
  language: string | null;       // ISO 639-1, null = any
  text: string | null;           // "Describe it your way", ≤ 280 chars — user text goes to the LLM, nothing else (docs/ai.md)
}
type CreateDeckRequest = { source: DeckSource; mood: MoodInput | null }   // mood required iff source = "mood" (400 errors.mood)
type MoodQueryDto = MoodInput & {
  id: string; filters: MoodFiltersDto;
  textIgnored: boolean;   // true: the text wasn't a mood, so only the chips were used (ADR-0022)
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
type TopPickDto = { film: FilmDto; reason: string | null }                          // reason: BE-13, null until then
```
Rules (server-side; the UI never decides them):
- `seenVerdict` only with `up` (else 400 `errors.seenVerdict`).
- **Library effects** of a swipe: `up` → `seen = true`; `up` + `loved` → also `liked = true`. `right` and `left` don't
  touch the library: a right swipe is a deck "yes", not a watchlist add (A2, decided). Saving to the watchlist is a
  separate button on the card (`PUT`/`DELETE /api/library/{tmdbId}/watchlist`, §7).
- The top 3 is chosen from the deck's right swipes only. Until BE-13 (Wikidata-based ranking/explanations) it is
  ordered by our own data (genre fit with the mood/swipes, then TMDB popularity) with `reason = null`.
- Mood chips map to our genres/filters on the server; free `text` goes through `mood-parse.v2`. `language` and
  `maxRuntime` are plain filters, not LLM input.
- **Unclear, off-topic or injected text (ADR-0022):** empty text makes no LLM call. Text the model labels `unclear` or
  `offTopic` → with chips: the deck is built from the chips and `mood.textIgnored = true`; without chips: 422
  `mood.unclear`, nothing created. No response or log ever repeats the user's text.
- Solo decks require a signed-in user; no anonymous "Just swipe" (A3, decided).
- Routes (A4, decided): `/mood` (picker), `/deck/:deckId` (swipe), `/deck/:deckId/top` (top 3; it belongs to a deck,
  "from 14 swipes").

## 6. Rooms and votes (ADR-0012, ADR-0015, docs/realtime.md)

A host (signed-in user) creates a room; friends open the link (`/rooms/{code}`), see a preview, and join as guests or
users. Mood rooms have a mood step in the lobby. The host starts; everyone swipes the shared deck; the server decides
match or best compromise. Codes look like `POP-428` (3 letters, dash, 3 digits), case-insensitive.

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `POST /api/rooms` | user | `CreateRoomRequest` | 201 `RoomDto`, `Location: /api/rooms/{code}` | 400, 401 |
| `GET /api/rooms/{code}/preview` | anon | — | 200 `RoomPreviewDto` | 404 `room.not_found` |
| `POST /api/rooms/{code}/join` | anon | `JoinRoomRequest` | 200 `RoomDto` (+ guest cookie when anonymous) | 400, 404, 409 `room.closed` / `room.full` |
| `GET /api/rooms/{code}` | member | — | 200 `RoomDto` | 401, 403 `room.not_member`, 404 |
| `PUT /api/rooms/{code}/mood` | member | `SetRoomMoodRequest` | 200 `RoomMoodDto` (now ready) | 400, 401, 403, 404, 409 `room.invalid_state`, 422 `mood.unclear` / `ai.refused`, 502, 503 |
| `POST /api/rooms/{code}/start` | host | — | 200 `RoomDto` (`status: "swiping"`) | 401, 403 `room.not_host`, 404, 409 `room.invalid_state` |
| `GET /api/rooms/{code}/deck?fromPosition=0` | member | — | 200 `DeckItemDto[]` (`swipe` = my vote) | 401, 403, 404, 409 `room.invalid_state` (lobby) |
| `POST /api/rooms/{code}/votes` | member | `CastVoteRequest` | 204 | 400, 401, 403, 404 / `deck.item_not_found`, 409 `room.already_voted` / `room.invalid_state` / `room.closed` |
| `GET /api/rooms/{code}/progress` | member | — | 200 `RoomProgressDto` | 401, 403, 404 |
| `POST /api/rooms/{code}/finish` | host | — | 200 `RoomResultDto` (compromise if no match yet) | 401, 403 `room.not_host`, 404, 409 `room.invalid_state` |
| `GET /api/rooms/{code}/result` | member | — | 200 `RoomResultDto` | 401, 403, 404, 409 `room.no_result` |
| `POST /api/rooms/{code}/leave` | member | — | 204 | 401, 403, 404 |
| `POST /api/rooms/{code}/close` | host | — | 204 | 401, 403 `room.not_host`, 404 |

```ts
type RoomMode   = "justSwipe" | "mood"        // "taste" (group taste) is added by the taste task
type RoomStatus = "lobby" | "swiping" | "result" | "closed"
type CreateRoomRequest = { name: string; mode: RoomMode; startsAt: string | null }   // name 1–40 ("Friday Film Club")
type JoinRoomRequest   = { displayName: string | null }   // required (1–30) when anonymous; ignored for users
type SetRoomMoodRequest = { moods: Mood[]; maxRuntime: RuntimeLimit; hardNos: HardNo[]; text: string | null }
type RoomMoodDto = { member: RoomMemberDto; textIgnored: boolean }   // textIgnored is only ever sent to that member (ADR-0022)
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
}                   // who said no is never revealed, only how many (A7)
```
Rules (all in services; see docs/realtime.md for when each event is published):
- **Join** is idempotent (already a member → 200). Late arrivals may join while `swiping` ("Late arrivals can still hop in").
  Joining publishes `memberJoined`; leave publishes `memberLeft`; close publishes `roomClosed`.
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

## 7. Library — My Cinema (UserFilm, ADR-0019)

One `UserFilm` per user and film, with independent flags (the Details A board toggles Like, Saved, Seen it) and a 1–5
rating in ticket stubs. There is no "Watching" state (A9, decided: dropped).

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
type UpdateUserFilmRequest = { liked: boolean; onWatchlist: boolean; seen: boolean; rating: number | null }  // rating 1–5
type UserFilmDto = UpdateUserFilmRequest & { film: FilmSummaryDto; updatedAt: string }
type LibrarySummaryDto = { liked: number; watchlist: number; seen: number }
```
- `PUT` replaces the whole state (last write wins); all false + `rating: null` deletes the row. The `/watchlist`
  pair changes only that flag, for the deck card's watchlist button, where the client doesn't hold the full state.
- A rating implies `seen = true` (server rule; A8, decided). `liked` stays a separate flag; the Liked shelf shows the
  rating when there is one.
- ADR-0019 lands here: referenced films are refreshed by the sync, never purged; FKs to `films` are `ON DELETE RESTRICT`.

## 8. Film details

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `GET /api/films/{tmdbId}` | anon | — | 200 `FilmDto` | 404 `film.not_found` — exists |
| `GET /api/films/{tmdbId}/trailer` | anon | — | 200 `FilmTrailerDto` | 404 `film.not_found`, 503 `tmdb.unavailable` |

```ts
type FilmTrailerDto = { trailer: { site: "YouTube"; key: string; name: string } | null }
```
- The trailer comes from TMDB on first request and is cached (ADR-0003), at most 180 days (ADR-0017). Never on the swipe path.
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

### As built (BE-01)
The stubs follow this file exactly. Names the shapes above leave implicit, now fixed in `contracts/openapi.json`:
- `"user" | "guest"` (`CurrentUserDto.kind`, `RoomMemberDto.kind`) is the enum `PrincipalKind`; `RoomResultDto.kind` is
  `RoomResultKind`; `RoomProgressDto.members[]` is `MemberProgressDto`; `FilmTrailerDto.trailer` is `TrailerDto` with
  `site: TrailerSite` (`"YouTube"`, TMDB's spelling kept).
- `MoodInput` keeps its name (it is part of `CreateDeckRequest`, not a request of its own).
- Browse and library sorts are the enums `FilmSort` and `LibrarySort`; query parameters are camelCase like JSON.
- Stubbed actions also declare `501` (`[ContractStub]`); a feature task removes the attribute when it lands.

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
| A11 | Group taste ("Blend your histories") with guests who have no history | Second wave, with the taste task |
| A12 | The glossary has no names for a solo deck's parent record or a solo swipe (`Vote` is a room member's) | `swipe`/`SwipeDto`/`swipeId` in the contract; BE-08 proposes glossary entries |
| A13 | Room mood has no `language` (solo mood does); is it stored as a `MoodQuery`? | No language in rooms; stored per member as a `MoodQuery` |
| A14 | ADR-0019's "unavailable" films (TMDB 404) have no flag in `FilmDto` | Add `isAvailable` additively in BE-05 if screens need it |
