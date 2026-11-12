---
description: Add an EF Core migration for a model change and verify it against pgvector Postgres
argument-hint: <MigrationName>
---

Create the migration `$ARGUMENTS`.

1. Make sure the model change is complete: entity in `Ithaca.Domain`, `DbSet` on `IAppDbContext` and `AppDbContext`,
   an `IEntityTypeConfiguration` in `Ithaca.Infrastructure/Persistence/Configurations/` (table name, keys, lengths, indexes
   with explicit names; vectors as `vector(Film.EmbeddingDimensions)`-style column types).
2. `dotnet tool restore` then
   `dotnet ef migrations add $ARGUMENTS --project src/backend/Ithaca.Infrastructure --startup-project src/backend/Ithaca.Api --output-dir Migrations`.
3. Read the generated `Up`/`Down`. Never hand-edit an applied migration; add a new one instead.
4. `docker compose up -d db`, export `.env` (`set -a; source .env; set +a`), then `dotnet ef database update …` (same flags).
5. `dotnet test tests/backend/Ithaca.Tests` (Testcontainers applies all migrations to a fresh pgvector database).
6. Finish with `/report`, listing the migration commands and their results.
