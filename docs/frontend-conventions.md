# Frontend conventions

Rules are in AGENTS.md §2; this page explains how to follow them. Copy the film slice:
`src/features/films/{api/useFilm.ts, pages/FilmPage.tsx, types.ts, index.ts}`.

## Folders
```
src/app/            composition: providers, router, routes.tsx, layout/ (AppShell, AppHeader, MenuSheet, navigation.ts),
                    pages/ (NotFound), dev/ (plumbing status page), PlaceholderPage
src/shared/api/     client.ts (openapi-fetch; the only fetch), apiError.ts, problem.ts (code → user text), schema.d.ts (generated)
src/shared/query/   queryClient.ts (the one QueryClient config), queryKeys.ts (every key)
src/shared/realtime/ events.ts (from contracts/realtime-events.json), roomHub.ts, useRoomHub.ts
src/shared/ui/      primitives: Wordmark, icons, LoadingState, ErrorState (add shared pieces here, not page-local copies)
src/shared/notify/  notify.ts (toasts), Toaster.tsx — the only files importing sonner
src/shared/styles/  tokens.css (design tokens), global.css (base styles)
src/features/<f>/   api/ (hooks) · components/ · pages/ · types.ts · index.ts (public surface)
```
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

## Real-time
- `useRoomHub({ enabled, handlers })` opens the single RoomHub connection for the screen and returns `{ status }`.
  Handlers are keyed by contract event names and usually invalidate queries. Details: docs/realtime.md.

## Toasts
- `notify.success / info / error / apiError`. Errors persist until dismissed. Never import `sonner` (lint error).

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

## Layout and responsiveness
- Mobile first: write for 375px, add `md:`/`lg:` overrides. Breakpoints: `sm` 30rem, `md` 48rem, `lg` 64rem, `xl` 80rem.
- Navigation: top header (wordmark · ☰; no bell until notifications exist, ADR-0014). Every page shows the TMDB attribution footer (`AppFooter`). The ☰ opens `MenuSheet` (native modal `<dialog>`); from `lg` the main items
  show as pills in the header. Items are defined once in `app/layout/navigation.ts`. No bottom tab bar.
- Deck gestures own the lower screen; toasts sit top-center.

## Accessibility
- Real `<button>`/`<a>`; icon-only controls get `aria-label`; visible focus (global pink-text ring); ≥ 44px targets;
  respect `prefers-reduced-motion`; colour pairs at least 4.5:1 (pink `#F04483` is a surface, not a text colour).

## Tests and checks
- `npm run typecheck && npm run lint && npm test && npm run build`. Vitest (node environment) for pure logic next to the
  file (`*.test.ts`). Add jsdom + Testing Library when the first interactive component needs it (ADR-0010).
