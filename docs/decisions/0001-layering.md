# ADR-0001: Modular monolith with four backend layers (mirrors FlowOps)

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Ithaca needs clear places for business rules, persistence and external AI/TMDB clients so many agents can work in parallel without eroding the design. FlowOps solved this with four projects and no repositories.

## Decision
- One repository, one API, one database (modular monolith). Feature folders inside each layer instead of separate services.
- Backend projects: `Ithaca.Domain` (entities, no dependencies) ← `Ithaca.Application` (services, DTOs, Result/Error, interfaces) ← `Ithaca.Infrastructure` (EF Core, pgvector, clients, jobs) ← `Ithaca.Api` (controllers, hub, composition). Plus `Ithaca.Tests` and `Ithaca.Evals`.
- Data access through `IAppDbContext` (EF Core `DbSet`s + `SaveChangesAsync`) used only by Application services; no generic repositories, no MediatR, no AutoMapper (FlowOps ADR-0007).
- One DI extension per layer (`AddApplication`, `AddInfrastructure`); services are concrete sealed classes.

## Consequences
+ Same shape as FlowOps: patterns and reviewers transfer directly.
+ Architecture tests enforce the direction.
- Application references EF Core abstractions; swapping ORMs would be work (not a requirement).

## Alternatives rejected
Microservices (no scale need, more moving parts); repository-per-aggregate (duplicates EF, blocks projections); vertical-slice single project (weaker enforcement of the AI/infra boundary).
