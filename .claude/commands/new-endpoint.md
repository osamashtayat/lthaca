---
description: Add one API endpoint following the reference slice (controller → service → data → DTO)
argument-hint: <feature> <METHOD /api/path> [what it does]
---

Add the endpoint `$ARGUMENTS` to Ithaca. Follow AGENTS.md §2–3 and docs/backend-conventions.md; copy the shape of the
film reference slice (`FilmsController`, `FilmQueryService`, `FilmDto`, `FilmErrors`, `FilmsEndpointTests`).

Steps, in order:
1. Read `docs/glossary.md` and the feature's `Ithaca.Application/<Feature>/README.md` (if present). Use glossary names.
2. **Application** (`src/backend/Ithaca.Application/<Feature>/`):
   - input `…Request` record with DataAnnotations (if it has a body/query), output `…Dto` record;
   - new error codes in `Ithaca.Domain/Errors/ErrorCodes.cs` and a `<Feature>Errors` class;
   - a method on a sealed `<Feature>…Service` returning `Result<T>` (or `Result`). All rules live here.
     Reads: `AsNoTracking().Select(new XDto(...))`. Writes: load → change entities → one `SaveChangesAsync` → then publish realtime.
   - register a new service in `Ithaca.Application/DependencyInjection.cs`.
3. **Api** (`src/backend/Ithaca.Api/Controllers/<Feature>Controller.cs`): one action, one service call,
   `this.ToActionResult(...)` / `ToCreatedResult(...)`. Add `[ProducesResponseType]` for success and each error status.
   Map new codes in `Errors/ErrorStatusCodes.cs`.
4. **Frontend messages:** add each new code to `src/frontend/src/shared/api/problem.ts`.
5. **Tests** in `tests/backend/Ithaca.Tests/<Feature>/`: success + each error path (use `IthacaApiFactory` for HTTP tests).
6. Run `./scripts/export-openapi.sh`, then `cd src/frontend && npm run api:types`.
7. Verify: `dotnet build Ithaca.slnx` and `dotnet test tests/backend/Ithaca.Tests` (all green).
8. Run the `architecture-reviewer` agent on the change, fix "Must fix" items, then finish with `/report`.

Don't: put logic, DbContext or try/catch in the controller; return an entity; add a repository or mapper.
