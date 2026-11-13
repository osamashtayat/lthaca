# Frontend conventions

Rules are in AGENTS.md §2; this page explains how to follow them. Copy the film slice:
`src/features/films/{api/useFilm.ts, pages/FilmPage.tsx, types.ts, index.ts}`.

## Folders
```
src/app/            composition: providers, router, routes.tsx, layout/ (AppShell, AppHeader, MenuSheet, navigation.ts),
                    pages/ (NotFound), dev/ (plumbing status `/dev/status`, UI kit gallery `/dev/ui`), PlaceholderPage
src/shared/api/     client.ts (openapi-fetch; the only fetch), apiError.ts, problem.ts (code → user text), schema.d.ts (generated)
src/shared/query/   queryClient.ts (the one QueryClient config), queryKeys.ts (every key)
src/shared/realtime/ events.ts (from contracts/realtime-events.json), roomHub.ts, useRoomHub.ts
src/shared/ui/      the UI kit (add shared pieces here, not page-local copies): Button, IconButton, Chip/ToggleChip,
                    SegmentedControl, StubRating, TicketCard, Poster, Sheet, MonoLabel, TextField + Wordmark, icons, LoadingState, ErrorState
src/shared/notify/  notify.ts (toasts), Toaster.tsx — the only files importing sonner
src/shared/styles/  tokens.css (design tokens), global.css (base styles)
src/mocks/          MSW mock API for the dev server and Vitest (ADR-0020); src/test/setup.ts starts it in Vitest
src/features/<f>/   api/ (hooks) · components/ · pages/ · types.ts · index.ts (public surface)
```
Film details (`features/films`) reads the caller's library state through `features/library`'s hooks (`useUserFilm`, `useUpdateUserFilm`: optimistic `PUT` with rollback + toast); visitors' actions go to `/sign-in?next=…`.
Features: `tonight`, `deck`, `rooms`, `mood`, `taste`, `library`, `browse`, `profile`, `import`, `auth` (+ `films`, the slice).
A feature imports `@/shared/*` and other features' `index.ts` only.

## Data
- Every server call is a hook in `features/<f>/api/`: `useQuery`/`useMutation` + `api.GET/POST…` + `requireData` +
  `queryKeys.<feature>…`. Components and pages never call `api` or `fetch` directly.
- Types: `components['schemas']['XDto']` from `@/shared/api` (regenerate with `npm run api:types` after
  `./scripts/export-openapi.sh`). Never hand-write a DTO type.
- Errors arrive as `ApiError` (`status`, `code`, `fieldErrors`). Show `toUserMessage(error)` (via `ErrorState` or
  `notify.apiError(error)`); show field errors with `serverFieldError(error, 'field')`. Never render the server's `detail`.
- Defaults (`createQueryClient`): 30 s stale time, one retry except 4xx, mutations never retry (a swipe or vote must not be
  sent twice). Invalidate by key prefix (`queryKeys.films.all`).

## Mock API
The dev server answers `/api` from MSW handlers (ADR-0020), so a screen can be built before its backend task is done.
`VITE_API_MOCKS=off npm run dev` talks to the real API (start it per AGENTS.md §1). Vitest always uses the same handlers;
a request without a handler fails the test. Production builds contain no mock code (`check-no-mocks`, run by `npm run build`).

Adding mocks for a frontend task:
1. The endpoint must be in `contracts/openapi.json`. If it isn't, open a `contract` issue (AGENTS.md §4.3); don't mock it.
2. In `src/mocks/handlers.ts`, add a block for the area:
   ```ts
   const libraryHandlers = [
     http.get(apiPath('/api/library/{tmdbId}'), ({ params }) => {
       const item = findMockUserFilm(Number(params.tmdbId))          // from libraryStore.ts
       return item
         ? HttpResponse.json<ResponseOf<'/api/library/{tmdbId}', 'get', 200>>(item)
         : problem(404, 'film.not_found')
     }),
   ]
   export const handlers = [...healthHandlers, ...filmHandlers, ...libraryHandlers]
   ```
   `apiPath` only accepts generated paths; `ResponseOf`/`RequestOf` type bodies from `schema.d.ts`; `problem` only accepts
   codes from `problem.ts`. Mutable state goes in `src/mocks/<area>Store.ts` with a `reset…()` called in `src/test/setup.ts`.
3. Keep mocks dumb: store and return contract shapes, and the few rules a screen needs to be exercised (e.g. 409 on a
   repeated vote). Don't reimplement ranking or matching; script outcomes in the store instead.
4. Cover error states the screen handles (each `code` from the contract) and override per test with `server.use(...)`.

Realtime stays off while mocks answer (`REALTIME_ENABLED`); test event reactions by calling the handlers you pass to
`useRoomHub`.

## Session and guards
- `useCurrentUser()` (features/auth) is the session: a `CurrentUserDto` (user or guest) or `null` (a 401 is an answer, not an
  error). A guest counts as signed out for user-only screens (`isUser`).
- User-only routes go inside the `RequireUser` layout group in `app/routes.tsx`; it redirects to `/sign-in?next=…`
  (`next` is sanitised by `safeNextPath`). The server still enforces access with 401.
- `shared/api/client.ts` echoes the `XSRF-TOKEN` cookie as `X-XSRF-TOKEN` on every POST/PUT/PATCH/DELETE; hooks do nothing.
- Sign-in/register/sign-out hooks update the `auth.me` cache and refetch or drop every other cached answer.
- Mocks: signed in as the mock user by default; tests call `setMockSession(null | MOCK_GUEST | user)` (reset after each test).

## Real-time
- `useRoomHub({ enabled, handlers })` opens the single RoomHub connection for the screen and returns `{ status }`.
  Handlers are keyed by contract event names and usually invalidate queries. Details: docs/realtime.md.

## Toasts
- `notify.success / info / error / apiError`. Errors persist until dismissed. Never import `sonner` (lint error).

## Design reference
The visual design is the owner's Claude design canvas **"Ithaca — Design Exploration"**:
https://claude.ai/artifact/R3jbc7WtrrDyGKrhjQJJAR (other agents may not be able to open it; this section is what you
need). Tokens are already in `src/shared/styles/tokens.css` (table below).

**Visual language**
- Ink outlines: 2–2.5px `border-ink` on cards, buttons and inputs; hard offset shadows (`shadow-hard`, `shadow-hard-yellow`),
  never blurred.
- Ticket-style cards: a dashed tear line (`border-dashed`) across the card, half-circle notches on both edges at the tear,
  and an "ADMIT 1" stub (DM Mono label + Shrikhand numeral); used for "By mood", "By taste", the room header (code on the stub).
- Labels: DM Mono, uppercase, letter-spaced ("JUST SWIPE", "OR GIVE THE DECK A HEAD START", "2 OF 3 READY").
- Headings: Bricolage Grotesque 800 with tight letter-spacing; Shrikhand per the Fonts rule below (e.g. "It's a match!",
  stub numerals).
- Pill buttons (`rounded-full`) for primary actions and toggles; big dark (`bg-ink text-cream`) bottom CTAs, 58–60px tall;
  segmented controls on `bg-beige`; selected chips turn ink with a yellow hard shadow.
- Ratings are five ticket stubs (pink when on), not stars, for the user's own rating, in half steps 0.5–5 (half a stub
  pink; ADR-0024, FE-20); TMDB scores show as "★ 8.0".
- Phone boards are 390×844; desktop boards (≥ lg) widen the same pages, they are not separate routes.

**Directions:** two screens have two directions; the owner chose **A** for both. Tonight = the "Tonight — Just me" and
"Tonight — With friends" boards (ignore "Tonight — Direction B (refined)"); Film details = "Film details (A)" (ignore
"Film details — Direction B"). Both details boards show "Watch on Mubi / Rent on Apple TV / Open in Letterboxd": that is
where-to-watch data, **not in scope** (backlog P-01); leave it out.

**Dropped from the boards:** the "Watching" tab on My Cinema and the "Keep watching · 42 min left" row on Tonight
(owner decision, api-contract A9). Build neither.

**Screen → route map**
| Board | Route | Backlog |
|---|---|---|
| Welcome / sign in | `/sign-in` | FE-03 |
| Tonight — Just me / With friends | `/` | FE-12 |
| Tonight — Direction B (refined) | — (not used) | — |
| Menu (☰ open) | `MenuSheet` | exists |
| Solo — by mood | `/mood` | FE-06 |
| Solo — the deck | `/deck` → `/deck/:deckId` | FE-05 |
| Solo — your top 3 | `/deck/:deckId/top` (decided, api-contract A4) | FE-05 |
| Solo — by taste | `/taste` | FE-13 |
| Film details (A) | `/films/:tmdbId` | FE-04 |
| Group — lobby | `/rooms/:code` | FE-09 |
| Group — your mood tonight | `/rooms/:code` (step, mood rooms) | FE-10 |
| Group — swipe | `/rooms/:code/swipe` | FE-11 |
| Group — it's a match / close call | `/rooms/:code/result` | FE-11 |
| My Cinema | `/cinema` | FE-07 |
| Browse | `/browse` | FE-08 |
| Import from Letterboxd | `/import` | FE-14 |
| Profile & settings | `/profile` | FE-15 |
| Notifications | dropped (ADR-0014) | — |
| Desktop — Tonight / Desktop — group swipe | same routes, ≥ lg layout | FE-12 / FE-11 |

Copy on the boards (names like "Lina", "Friday Film Club", fictional films and posters) is sample content, not product
data. Sample text is fine in mocks; real screens show API data.

## Design tokens (from the design canvas)
| Token | Value | Tailwind | Use |
|---|---|---|---|
| cream | `#F8F5EC` | `bg-cream` | page background |
| yellow | `#F5D94E` | `bg-yellow` | active/selected, highlight |
| pink | `#F04483` | `bg-pink` | accent surfaces, wordmark shadow — not text |
| ink | `#242321` | `text-ink border-ink bg-ink` | text, 2–2.5px borders, dark surfaces |
| beige | `#EAE3D5` | `bg-beige` | soft panels, segmented controls |
| pink text | `#C8245F` | `text-pink-text` | readable pink: links, hover, focus ring |
| ink muted / line / cream soft | `#5B574F` / `#D8D0BF` / `#E4DED0` | `text-ink-muted`, `border-line`, `text-cream-soft` | secondary text, dividers, text on ink |

Fonts: `font-sans` Bricolage Grotesque (all UI) · `font-display` Shrikhand (wordmark and big moments only) ·
`font-mono` DM Mono (metadata, labels like "PICK A FILM"). Self-hosted via @fontsource (no third-party requests).
Shape: `rounded-control` 14px · `rounded-tile` 18px · `rounded-card` 22px · `rounded-hero` 24px · `rounded-full` pills;
`shadow-hard` (5px 5px 0 ink) and `shadow-hard-yellow`; no blurred shadows.
Layout vars: `--header-height` 64px (phones) / `--header-height-lg` 76px, `--content-max-width` 1240px, `--gutter` 20px,
`--touch-target` 44px.
`npm run lint:tokens` rejects hex/rgb literals and Tailwind default palette classes in `.ts/.tsx`.

Use the kit (`@/shared/ui`: `Button`, `IconButton`, `ToggleChip`, `Sheet`…) instead of hand-styled `<button>`s or dialogs; extend a kit piece rather than copying it.

## Layout and responsiveness
- Mobile first: write for 375px, add `md:`/`lg:` overrides. Breakpoints: `sm` 30rem, `md` 48rem, `lg` 64rem, `xl` 80rem.
- Navigation: top header (wordmark · ☰; no bell until notifications exist, ADR-0014). Every page shows the TMDB attribution footer (`AppFooter`). The ☰ opens `MenuSheet` (native modal `<dialog>`); from `lg` the main items
  show as pills in the header. Items are defined once in `app/layout/navigation.ts`. No bottom tab bar.
- Deck gestures own the lower screen; toasts sit top-center.

## Help sheets ("?")
When a screen asks the user to do something outside Ithaca (first case: getting the Letterboxd export, ADR-0024), it
gets a 44px `IconButton` "?" (`aria-label` names the topic, e.g. "How to get your Letterboxd file") next to the action,
opening a `Sheet` with short numbered steps: one action per step, the exact menu names of the other service (verified on
its help pages, never guessed), phone first, and one line on what we do with the data. The steps live once in the
feature that owns the topic (`LetterboxdHelpSheet` in `features/import`, exported from its `index.ts`) and every screen
that needs them reuses it (FE-13's empty state, FE-14's `/import`).

**Import entry points** (where they are: ADR-0024 → Where users meet it): each is a link to `/import`; none uploads by
itself or reimplements the help steps.

## Accessibility
- Real `<button>`/`<a>`; radio-style widgets (SegmentedControl, StubRating) are native `<input type="radio">` (visually
  hidden) so the browser provides arrow-key selection and the single tab stop; icon-only controls get `aria-label`; visible focus (global pink-text ring); ≥ 44px targets;
  respect `prefers-reduced-motion`; colour pairs at least 4.5:1 (pink `#F04483` is a surface, not a text colour).

## Tests and checks
- `npm run typecheck && npm run lint && npm test && npm run build`. Vitest (node environment) for pure logic next to the
  file (`*.test.ts`), with the MSW node server from `src/test/setup.ts` for anything that calls the API.
- Component tests use jsdom + Testing Library (FE-02): put `// @vitest-environment jsdom` on the first line of the
  `*.test.tsx` file (everything else stays in node) and call `cleanup` in `afterEach`. jsdom has no modal `<dialog>`;
  `src/test/setup.ts` installs a minimal `showModal`/`close`, so focus trapping isn't covered by tests: check it in the
  browser at `/dev/ui`.
