# Backend conventions

Rules are in AGENTS.md §2; this page explains how to follow them. Copy the film slice:
`Controllers/FilmsController.cs` → `Films/FilmQueryService.cs` → `FilmDto`, `FilmErrors` → `FilmsEndpointTests`.

## Controllers and hubs
- `[ApiController]`, `[Route("api/<plural>")]`, `[Tags("<Feature>")]`, sealed, primary-constructor injection of **services only**.
- One action = one service call, then `this.ToActionResult(result)` (200), `this.ToCreatedResult(result, v => url)` (201) or
  `this.ToActionResult(result)` for a non-generic `Result` (204). Nothing else: no `if`, no mapping, no try/catch.
- **Contract stubs** (BE-01): an endpoint whose task hasn't landed has its final route, DTOs and statuses, plus
  `[ContractStub]` (documents 501), and its service method returns `ContractStub.Of<T>()` / `ContractStub.Command()`
  (501 `server.not_implemented`). Implementing it: replace the body, drop `[ContractStub]` and the CA1822 suppression,
  remove its row from `ContractStubEndpointTests`, re-export the contract.
- List queries derive from `PageRequest` and bind camelCase names (`CamelCaseBindingMetadataProvider`).
- Declare `[ProducesResponseType]` for every status; they feed `contracts/openapi.json` and the frontend types.
- Route constraints (`{tmdbId:int:min(1)}`) and DataAnnotations on `…Request` records do input-shape validation
  (automatic 400). Business validation is the service's job and returns `Error.Validation(field, message)`.

## Services
- Live in `Ithaca.Application/<Feature>/`, `sealed`, named `<Feature><Query|Command|…>Service` or by use case; registered one
  per line in `Ithaca.Application/DependencyInjection.cs` (scoped).
- Return `Result<T>` / `Result` for anything a user can cause (not found, invalid, conflict, not allowed). Throw only for bugs.
- Errors: add a code to `Ithaca.Domain/Errors/ErrorCodes.cs` (`area.snake_case`), a static `Error` in `<Feature>Errors`,
  a status in `Ithaca.Api/Errors/ErrorStatusCodes.cs`, a message in `src/frontend/src/shared/api/problem.ts`.
  `ErrorStatusCodesTests` fails until all three exist.
- A service never calls another use-case service; share a rule by extracting it (a small class or a domain method).

## Result and errors (ADR-0004)
- `Result` / `Result<T>` in `Application/Common`. Implicit conversions: `return dto;` or `return FilmErrors.NotFound;`.
- `ResultExtensions` (Api) is the only place a Result becomes HTTP. `ProblemDetailsSetup` gives every error response
  `type`, `title`, `status`, `detail`, `code`, `traceId`. Unknown codes become 500 `server.error` with no internals.
- Entities can't return Results: they throw `DomainException(code, message, field)`; the global
  `ProblemDetailsExceptionHandler` maps it the same way. Any other exception = 500.

## Domain
- Entities in `Ithaca.Domain/<Feature>/`: private setters, private parameterless ctor for EF, static `Create(...)` that
  enforces invariants, ids from `SequentialGuid.NewV7(time)`, time passed in (from `TimeProvider`).
- Constants for limits (`Film.TitleMaxLength`, `Film.EmbeddingDimensions`) live on the entity; configurations use them.

## Persistence
- `IAppDbContext` exposes a `DbSet` per aggregate; no repositories (ADR-0001).
- Reads: `AsNoTracking()` + `Select(new XDto(...))`, order before paging, `Paging.ToPagedResultAsync(page, ct)` for lists
  (`PageRequest` is the base of every list query; `PagedResult<T>` is the response).
- Writes: load, call entity methods, `SaveChangesAsync` **once**, then publish realtime. Exception: a resumable batch job
  (e.g. `CatalogSyncService`) may save once per batch; say so in the service's doc comment.
- Configurations: `Infrastructure/Persistence/Configurations/<Entity>Configuration.cs`, explicit table and index names,
  snake_case via `EFCore.NamingConventions`. Vectors: `HasColumnType($"vector({N})")` + converter from `float[]` + HNSW index.
- Migrations: `/new-migration` or AGENTS.md §1. Generated files are `generated_code` in `.editorconfig`; never edit an
  applied one.

## Configuration
- One options class per section, implementing `IConfigSection` (`static string SectionName`), next to its consumer.
- Register with `services.AddValidatedOptions<T>(configuration)` (binds, validates DataAnnotations, fails at startup).
- Defaults in `appsettings.json`; secrets only via env vars / user-secrets; every key in `.env.example` with a placeholder.

## Logging
- JSON console logs. Use `[LoggerMessage]` source-generated methods. Never log user free text, mood text, emails or keys.

## Tests
- `tests/backend/Ithaca.Tests/<Area>/`, xUnit v3 + Shouldly, names `Method_Condition_Result`.
- HTTP/database tests: `[Collection(DatabaseCollectionDefinition.Name)]` + `IthacaApiFactory.StartAsync(postgres.ConnectionString)`
  (Testcontainers `pgvector/pgvector:pg17`, real migrations, dev seed: films 550, 27205, 496243).
- Guard tests already present: layer dependencies, no DbContext in controllers/hubs, sealed services, error-code mapping,
  frontend messages per code, OpenAPI drift, realtime contract drift, prompt files exist.
