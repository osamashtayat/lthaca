# ADR-0021: Contract first, one task per branch and worktree, backend and frontend agents in parallel

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Two AI agents (one backend, one frontend) will work through the backlog at the same time. Without a shared contract the
frontend waits or guesses; without branch rules they overwrite each other in the shared checkout. FlowOps solved both
(its ADR-0008, `docs/PHASE1_TASKS.md`, `docs/API_CONTRACT.md`, `docs/AGENT_HANDOFF.md`, AGENTS.md §4–5).

## Decision
- **Design contract:** `docs/api-contract.md` (first wave). **Executable contract:** `contracts/openapi.json`, generated;
  the frontend types come from it. The backend owns both; the frontend never edits them.
- **Contract tasks before frontend tasks:** a contract the frontend waits for lands as its own small merged task
  (BE-01 for the first wave, `BE-nna` later); frontend tasks depend on those, never on backend logic.
- **Stubs before logic:** backlog task BE-01 creates every first-wave endpoint with its final DTOs; until its feature task
  lands, the service returns `server.not_implemented` (501) through the normal `Result` pipeline. Controllers keep their
  final thin shape from day one. BE-17 removes the code at the end.
- **Mocks:** the frontend builds against MSW handlers typed from the contract (ADR-0020).
- **Backlog:** `docs/backlog.md`, tasks `BE-nn`, `FE-nn`, `OPS-nn`, proposals `P-nn`; status updated in the task's own PR.
- **Branch workflow (AGENTS.md §4):** one task = one branch `task/<ID>-short-name` from the latest `origin/main`, in its
  own worktree under `.claude/worktrees/` (git-ignored), claimed by a draft PR right after the first push; never switch branches or commit in the shared checkout; PR into
  `main`, CI green, squash-merge, delete the branch. Ownership by path: backend tasks touch `src/backend`, `tests/backend`,
  `prompts`, `evals`, `contracts`; frontend tasks touch `src/frontend` (incl. the regenerated `schema.d.ts` only via
  `npm run api:types`). Problems become GitHub issues with an area and a kind label.

### Deviations from FlowOps
- No `docs/AGENT_HANDOFF.md`: its reading order, ownership table and issue rules live in AGENTS.md §4 (ADR-0011: one rule
  source). The end-of-task report stays the AGENTS.md §5 format.
- Stubs return 501 through a `Result` error code instead of throwing (ADR-0004).
- File names: `docs/backlog.md` and `docs/api-contract.md` (lower-case, like the other docs).
- Worktrees live in `.claude/worktrees/` (already git-ignored) instead of an arbitrary sibling folder.

## Consequences
+ The frontend can finish screens before the backend logic; type errors reveal contract drift.
+ Agents never share a working tree; conflicts appear only at merge time, on a branch, where CI checks them.
- Contract changes cost a backend round-trip; the frontend must open an issue instead of adding a field.
- Parallel backend tasks that add EF migrations must merge one at a time (the later one regenerates its migration).

## Alternatives rejected
Frontend-led contract (the server's rules would be guessed); one long-lived branch per agent (drifts from `main`, big
merges); no stubs, mocks only (no generated types until each feature lands).
