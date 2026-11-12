# Architecture Decision Records

One file per decision: `NNNN-title.md` with Status (Proposed / Accepted / Superseded by ADR-XXXX), Context, Decision, Consequences, Alternatives rejected. To change a decision, add a new ADR that supersedes the old one; never rewrite an accepted ADR's decision.

ADRs marked *deviation* record where Ithaca differs from the FlowOps reference project and why.

| ADR | Decision |
|---|---|
| [0001](0001-layering.md) | Modular monolith with four backend layers (mirrors FlowOps) |
| [0002](0002-pgvector-in-postgres.md) | Embeddings stored in PostgreSQL with pgvector (dimension amended by 0013) |
| [0003](0003-tmdb-sync-not-live.md) | TMDB is synced nightly into our catalog, not called live |
| [0004](0004-result-type.md) | Expected failures are returned as Result, not thrown (deviation from FlowOps) |
| [0005](0005-frontend-structure.md) | Frontend `shared/` + feature folders with `api/` subfolders (deviation from FlowOps) |
| [0006](0006-sonner-toasts.md) | Toasts with sonner behind `shared/notify` (deviation from FlowOps) |
| [0007](0007-hub-in-api.md) | RoomHub and its publisher live in the Api (deviation from FlowOps) |
| [0008](0008-background-jobs.md) | Plain BackgroundService for the nightly sync; no job framework |
| [0009](0009-local-http-and-ports.md) | Plain HTTP locally; ports chosen to coexist with FlowOps (deviation from FlowOps) |
| [0010](0010-skeleton-dependencies.md) | Only the dependencies the skeleton uses (deviation from FlowOps; MSW part superseded by 0020) |
| [0011](0011-agent-files.md) | AGENTS.md as the only rule source; `.claude/commands` instead of rules/skills (deviation from FlowOps) |
| [0012](0012-auth-users-and-guests.md) | Cookie auth for users; guests as room-scoped cookie identities |
| [0013](0013-embeddings-voyage-1024.md) | Embeddings from Voyage AI at 1024 dimensions, model recorded per vector |
| [0014](0014-no-notifications-yet.md) | No notifications feature for now |
| [0015](0015-votes-over-http.md) | Votes are HTTP requests; the hub only broadcasts |
| [0016](0016-single-instance-assumptions.md) | One API instance for now; what to add before scaling out |
| [0017](0017-tmdb-terms-and-ai.md) | TMDB for display, Wikidata for AI (TMDB API terms) |
| [0018](0018-anthropic-llm-client.md) | Anthropic client: official SDK, schema-enforced JSON, Result errors, server-side fallback |
| [0019](0019-catalog-retention.md) | Films users reference are refreshed, never purged |
| [0020](0020-msw-mock-api.md) | Mock API with MSW for the dev server and Vitest (deviation from FlowOps: mocks everything when on) |
| [0021](0021-contract-first-parallel-agents.md) | Contract first, one task per branch and worktree, backend and frontend agents in parallel |
| [0022](0022-mood-input-guard.md) | Unclear, off-topic and injected mood text: the model labels, the server decides, chips win |
| [0023](0023-auth-as-built.md) | Auth as built: 30-day sliding sessions, Lax cookies, no antiforgery exemption, guests in the Identity scheme |
