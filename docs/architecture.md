# Architecture

Ithaca is a modular monolith in one repository: one ASP.NET Core API (four projects), one React SPA, one PostgreSQL
database with pgvector, and an eval runner. Mirrors FlowOps' layering (ADR-0001).

## Layers and dependency direction

```
            Ithaca.Domain            entities, value objects, ErrorCodes, DomainException — BCL only
                 ▲
            Ithaca.Application       services (all business rules), DTOs, Result/Error, interfaces:
                 ▲                   IAppDbContext, ITmdbClient, ILlmClient, IEmbeddingClient, IPromptLibrary, IRoomEventPublisher
                 │
            Ithaca.Infrastructure    AppDbContext + EF configurations + migrations + pgvector, Fake/real clients,
                 ▲                   FilePromptLibrary, background jobs, dev seeding, AddValidatedOptions
                 │
            Ithaca.Api               controllers, RoomHub + SignalR publisher, ProblemDetails/Result mapping,
                                     OpenAPI, Program.cs (composition root)
```

Arrows point at what a project may reference. Application never sees Infrastructure, Api, Npgsql or Pgvector;
`tests/backend/Ithaca.Tests/Architecture/LayerDependencyTests.cs` enforces it. The Api references Infrastructure **only**
to call `AddInfrastructure()`.

## Request flow (the reference slice)

```
GET /api/films/550
  → nginx (Docker) or Vite proxy (dev)          same origin, no CORS
  → FilmsController.Get(tmdbId)                 bind route value, nothing else
  → FilmQueryService.GetByTmdbIdAsync           the rule: lookup by TmdbId, Result<FilmDto> or FilmErrors.NotFound
  → IAppDbContext.Films (AppDbContext, EF+Npgsql) AsNoTracking().Select(new FilmDto(...))
  ← Result<FilmDto>
  → this.ToActionResult(result)                 200 + FilmDto, or ProblemDetails {code: film.not_found, traceId}
Frontend: FilmPage → useFilm (TanStack Query, queryKeys.films.detail) → api.GET (openapi-fetch, generated types)
          → ApiError on failure → ErrorState → toUserMessage(code)
```

Real-time follows the same rule: a service changes state, commits, then calls `IRoomEventPublisher`; `RoomHub` holds no
logic (docs/realtime.md). The nightly catalog sync: `CatalogSyncJob` (schedules) → `CatalogSyncService` (logic).

## Runtime

| Piece | Local dev | Docker (`docker compose up --build`) |
|---|---|---|
| PostgreSQL 17 + pgvector | `localhost:5433` | service `db` (`pgvector/pgvector:pg17`) |
| Migrations | `dotnet ef database update` | one-shot `migrate` (EF bundle) before `api` starts |
| API | `http://localhost:5280` | service `api`, port 8080, not published |
| SPA | Vite `http://localhost:5273` (proxies `/api`, `/hubs`) | service `web` (nginx) `http://localhost:8080` |

Configuration: `appsettings.json` holds non-secret defaults; secrets come from env vars (`.env`, see `.env.example`)
or user-secrets. Providers (`Ai:LlmProvider`, `Ai:EmbeddingProvider`, `Tmdb:Client`) default to `Fake`.

## Folder map

```
AGENTS.md CLAUDE.md          agent instructions (AGENTS.md is the source of truth)
.claude/                     settings, slash commands, architecture-reviewer agent, launch.json
.github/workflows/ci.yml     CI: backend, frontend, Docker stack smoke
contracts/                   openapi.json (generated), realtime-events.json (hub contract, hand-written)
prompts/                     versioned LLM prompts: <name>.v<N>.md
docs/                        this documentation, backlog.md (tasks), api-contract.md (design contract), decisions/ (ADRs)
scripts/export-openapi.sh    regenerates contracts/openapi.json
src/backend/
  Ithaca.Domain/             <Feature>/ entities · Common/ · Errors/
  Ithaca.Application/        <Feature>/ services+DTOs+errors · Common/ (Result, paging, IAppDbContext) · Ai/ · Tmdb/ · Realtime/
  Ithaca.Infrastructure/     Persistence/ · Migrations/ · Ai/ · Tmdb/ · Jobs/ · Seeding/ · Configuration/
  Ithaca.Api/                Controllers/ · Hubs/ · Errors/ · OpenApi/ · Program.cs
  Dockerfile
src/frontend/
  src/app/                   providers, router, routes, layout/ (AppShell, header, ☰ menu), pages/, dev/
  src/shared/                api/ · query/ · realtime/ · ui/ · notify/ · styles/
  src/mocks/                 MSW mock API for the dev server and Vitest (ADR-0020); never in production builds
  src/test/setup.ts          Vitest setup: MSW node server, unhandled requests fail
  src/features/<feature>/    api/ · components/ · pages/ · types.ts · index.ts
  Dockerfile docker/nginx.conf
tests/backend/Ithaca.Tests/  architecture, contract, error-mapping, AI plumbing, slice (Testcontainers) tests
evals/                       Ithaca.Evals runner + cases/*.json
```

Backend feature folders (Application): `Films` (reference slice), `Catalog`, `Decks`, `Rooms`, `Moods`, `Taste`, `Library`,
`Imports`, `Users`. Frontend feature folders: `films` (reference slice), `tonight`, `deck`, `rooms`, `mood`, `taste`,
`library`, `browse`, `profile`, `import`, `auth`.
