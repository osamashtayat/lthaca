# Architecture Decision Records

One file per decision: `NNNN-title.md` with Status (Proposed / Accepted / Superseded by ADR-XXXX), Context, Decision, Consequences, Alternatives rejected. To change a decision, add a new ADR that supersedes the old one; never rewrite an accepted ADR's decision.

ADRs marked *deviation* record where Ithaca differs from the FlowOps reference project and why.

| ADR | Decision |
|---|---|
| [0001](0001-layering.md) | Modular monolith with four backend layers (mirrors FlowOps) |
| [0002](0002-pgvector-in-postgres.md) | Embeddings stored in PostgreSQL with pgvector |
| [0003](0003-tmdb-sync-not-live.md) | TMDB is synced nightly into our catalog, not called live |
| [0004](0004-result-type.md) | Expected failures are returned as Result, not thrown (deviation from FlowOps) |
| [0005](0005-frontend-structure.md) | Frontend `shared/` + feature folders with `api/` subfolders (deviation from FlowOps) |
| [0006](0006-sonner-toasts.md) | Toasts with sonner behind `shared/notify` (deviation from FlowOps) |
| [0007](0007-hub-in-api.md) | RoomHub and its publisher live in the Api (deviation from FlowOps) |
| [0008](0008-background-jobs.md) | Plain BackgroundService for the nightly sync; no job framework |
| [0009](0009-local-http-and-ports.md) | Plain HTTP locally; ports chosen to coexist with FlowOps (deviation from FlowOps) |
| [0010](0010-skeleton-dependencies.md) | Only the dependencies the skeleton uses (deviation from FlowOps) |
| [0011](0011-agent-files.md) | AGENTS.md as the only rule source; `.claude/commands` instead of rules/skills (deviation from FlowOps) |
