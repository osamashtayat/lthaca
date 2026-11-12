# Ithaca — Instructions for Coding Agents

Ithaca is a mobile-first website (≈95% phone users) that helps people pick a movie, alone or with friends: swipe a deck (right = I'd watch it, left = not for me, up = seen it), start it by **just swiping**, **by mood** (LLM turns words into filters) or **by taste** (history, Letterboxd import), or open a **group room** with a share link where friends (guests, no account) vote in real time until there's a match. Films come from our own TMDB-synced catalog in PostgreSQL with pgvector; the LLM only works at the edges and never invents films.

Stack: ASP.NET Core (.NET 10) · EF Core + Npgsql + pgvector · SignalR · React 19 + TypeScript (strict) + Vite + Tailwind v4 + TanStack Query · Docker.

This file is the single source of truth for every agent (Claude, Codex, Cursor…). Detail lives in `docs/`; don't duplicate it here.

| Read | When |
|---|---|
| [docs/architecture.md](docs/architecture.md) | Always, once: layers, dependency direction, request flow, folder map |
| [docs/backend-conventions.md](docs/backend-conventions.md) | Any backend change |
| [docs/frontend-conventions.md](docs/frontend-conventions.md) | Any frontend change |
| [docs/ai.md](docs/ai.md) | LLM, prompts, embeddings, evals |
| [docs/realtime.md](docs/realtime.md) | Rooms, RoomHub, events |
| [docs/glossary.md](docs/glossary.md) | Domain names — use them exactly |
| [docs/decisions/](docs/decisions/README.md) | Why things are the way they are (ADRs) |

## 1. Commands

Run from the repo root unless noted. First time: `cp .env.example .env`.

```bash
docker compose up --build                     # full stack: db + migrations + api + web → http://localhost:8080
docker compose up -d db                       # only PostgreSQL + pgvector (localhost:5433) for local dev
set -a; source .env; set +a                   # export ConnectionStrings__Ithaca etc. for dotnet commands
dotnet run --project src/backend/Ithaca.Api   # API → http://localhost:5280 (Development seeds 3 films)
cd src/frontend && npm install && npm run dev # SPA → http://localhost:5273 (proxies /api and /hubs to :5280)

dotnet build Ithaca.slnx                      # backend build (warnings are errors)
dotnet test tests/backend/Ithaca.Tests        # unit + architecture + contract + slice tests (needs Docker for Testcontainers)
cd src/frontend && npm run typecheck && npm run lint && npm test && npm run build

dotnet tool restore                           # once: dotnet-ef
dotnet ef migrations add <Name> --project src/backend/Ithaca.Infrastructure --startup-project src/backend/Ithaca.Api --output-dir Migrations
dotnet ef database update --project src/backend/Ithaca.Infrastructure --startup-project src/backend/Ithaca.Api

dotnet run --project src/backend/Ithaca.Api -- --sync-catalog   # one TMDB catalog sync, then exit
dotnet user-secrets --project src/backend/Ithaca.Api set "Ai:ApiKey" "…"   # keys: user-secrets locally, .env in Docker (docs/ai.md)

./scripts/export-openapi.sh                   # after any API change → contracts/openapi.json
cd src/frontend && npm run api:types          # then regenerate src/shared/api/schema.d.ts

dotnet run --project evals/Ithaca.Evals                 # AI evals, recorded replies (no network)
dotnet run --project evals/Ithaca.Evals -- --live       # AI evals against the configured provider
```

Plumbing check page: `/dev/status` on the Vite dev server only (:5273; not in production builds). Reference slice: `/films/550`.

## 2. Architecture rules

### Backend — do
- **Controllers are thin.** An action: receive the request DTO → call **one** service method → `this.ToActionResult(result)`. Copy [FilmsController](src/backend/Ithaca.Api/Controllers/FilmsController.cs).
- **Hubs are thin.** `RoomHub` methods call one service and return. Jobs (`BackgroundService`) only schedule and call one service.
- **Services own the logic.** All business rules live in sealed services in `Ithaca.Application/<Feature>/`. Services depend on interfaces (`IAppDbContext`, `ITmdbClient`, `ILlmClient`, `IEmbeddingClient`, `IPromptLibrary`, `IRoomEventPublisher`), never on Infrastructure classes.
- **Expected failures are `Result` errors**, defined once per feature (`FilmErrors`). Codes live in `ErrorCodes` (`area.snake_case`) and are mapped to HTTP status in `ErrorStatusCodes`, and to user text in `src/frontend/src/shared/api/problem.ts`. Tests fail if one of the three is missing.
- **Data access:** `IAppDbContext` inside services only; reads project to DTOs with `AsNoTracking().Select(...)`; one `SaveChangesAsync` per use case; publish real-time events **after** it.
- **One way for each concern:** paging → `PageRequest` / `Paging.ToPagedResultAsync`; config → an `IConfigSection` options class + `AddValidatedOptions<T>()`; DI → `AddApplication()` / `AddInfrastructure()` (one line per service); errors → `Result` + the global `ProblemDetailsExceptionHandler`; time → `TimeProvider`; ids → `SequentialGuid.NewV7`.
- **AI behind interfaces.** Prompts are files `prompts/<name>.v<N>.md`; outputs are parsed with `StructuredOutput.Parse<T>` into typed records. Every film id the LLM returns must be validated against the candidate list we sent.
- **Runtime film data comes from our `films` table.** TMDB is only for the nightly sync and cached details (trailers).
- **TMDB terms (ADR-0017, pending):** don't send TMDB-derived text to the LLM or the embedding model until the owner decides; keep the footer attribution.

### Backend — don't
- No `DbContext`, `if` business rules, try/catch, or mapping in controllers or hubs.
- No reference from Domain/Application to Infrastructure, Api, Npgsql or Pgvector (an architecture test enforces this).
- No generic repositories, MediatR, AutoMapper, or new abstractions without ≥ 2 real callers.
- No prompt text in C# strings, no live TMDB calls to build decks, no secrets in `appsettings*.json`.
- Never return an entity from a controller; never return an LLM-provided film id without validating it.

### Frontend — do
- **Feature folders:** `src/features/<feature>/{api/,components/,pages/,types.ts,index.ts}`. Other code imports a feature only through its `index.ts`.
- **Data goes through feature `api/` hooks** on TanStack Query + `api` from `@/shared/api` + keys from `queryKeys`. Copy [useFilm](src/frontend/src/features/films/api/useFilm.ts) and [FilmPage](src/frontend/src/features/films/pages/FilmPage.tsx).
- API types come from the generated `schema.d.ts` (`npm run api:types`), never hand-written.
- Toasts via `notify` from `@/shared/notify`; real-time via `useRoomHub` from `@/shared/realtime`; event names from `contracts/realtime-events.json`.
- Colours, fonts, radii, shadows only from `src/shared/styles/tokens.css` (Tailwind classes such as `bg-cream text-ink font-display shadow-hard rounded-card`). Mobile first; touch targets ≥ 44px; navigation is the top header + ☰ menu.

### Frontend — don't
- No `fetch` in components (lint error); no importing `sonner`, `@microsoft/signalr` or `openapi-fetch` outside `src/shared` (lint error).
- No hex/rgb colours or default Tailwind palette classes in `.ts/.tsx` (`npm run lint:tokens`).
- No bottom tab bar; no Shrikhand outside the wordmark and big moments.
- No business rules in components: anything the server decides (eligibility, matches, ranking) comes from the API.

## 3. Naming and where new code goes

- Domain names exactly as [docs/glossary.md](docs/glossary.md): `User`, `Guest`, `Film`, `UserFilm`, `Room`, `RoomMember`, `DeckItem`, `Vote`, `TasteProfile`, `MoodQuery`, `ImportJob`.
- C#: inputs `…Request`, outputs `…Dto`, services `…Service` (sealed), errors `<Feature>Errors`, async methods `…Async(…, CancellationToken ct)`. Tables snake_case plural.
- TS: hooks `useX` in `api/`, pages `XPage.tsx` in `pages/`, components PascalCase in `components/`.

| Adding… | Do this |
|---|---|
| an endpoint | `/new-endpoint`: DTOs + `<Feature>Errors` + service method in `Ithaca.Application/<Feature>/` → register in `AddApplication()` → thin action in `Ithaca.Api/Controllers/<Feature>Controller.cs` → status mapping for new codes → test → `export-openapi.sh` → `npm run api:types` |
| a table/entity | entity in `Ithaca.Domain/<Feature>/` → `DbSet` on `IAppDbContext` + `AppDbContext` → `<Entity>Configuration` → migration (`/new-migration`) |
| a feature page | `/new-feature`: page in `src/features/<f>/pages/`, hook in `api/`, keys in `shared/query/queryKeys.ts`, export from `index.ts`, swap the `PlaceholderPage` route in `src/app/routes.tsx` |
| a real-time event | name in `contracts/realtime-events.json` **and** `RoomEvents.cs` → publish from a service via `IRoomEventPublisher` after commit → handle with `useRoomHub({ handlers })` |
| a prompt / AI call | `prompts/<name>.v1.md` + id in `Prompts` + output record in `Ithaca.Application/Ai/` + an eval case in `evals/cases/` |
| an external provider | implement the interface in `Ithaca.Infrastructure/<Area>/`, add it to the config switch in `DependencyInjection.cs`, extend its startup validation (allowed + key required) — docs/ai.md |
| a config section | options class implementing `IConfigSection` next to its consumer + `AddValidatedOptions<T>()` + defaults in `appsettings.json`, secrets in `.env.example` (placeholder) |

## 4. Process

- Small, focused changes; no unrelated refactoring (note it as a TODO instead).
- Run the build and tests you touched; never claim a result you didn't see.
- CI (`.github/workflows/ci.yml`) runs the §1 checks on every PR and push to `main`: backend build/tests/evals, OpenAPI and migration drift, frontend type drift/typecheck/lint/test/build, and a Docker stack smoke test. A red CI is not done.
- If a rule here blocks you, don't work around it silently: say so in the report and propose an ADR in `docs/decisions/`.
- Before finishing a sizeable change, run the `architecture-reviewer` agent (Claude) or check §2 yourself.

## 5. Mandatory end-of-task report

After finishing any task, the agent must end its reply with a report in this format:

Summary: what was done, in 2–4 sentences
Files created: list of paths
Files changed: list of paths, with one line each on what changed
Files deleted: list of paths (or "none")
Commands run: build, test and migration commands, with results
Decisions made: anything that wasn't specified and why
Rule check: confirm controllers are thin, logic is in services, nothing is duplicated, or list exceptions
Open questions / TODOs: anything left for a human
