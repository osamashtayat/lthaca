# ADR-0005: Frontend `shared/` + feature folders with `api/` subfolders (deviation from FlowOps)

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
FlowOps uses `src/lib/` (API, query, realtime), `src/components/ui` and `features/<f>/api.ts`. The Ithaca brief specifies `src/shared/{api,query,realtime,ui,notify,styles}` and `src/features/<f>/{api/,components/,pages/,types.ts}`.

## Decision
- Follow the brief's layout. Each feature exposes its public surface through `index.ts`.
- Kept from FlowOps: openapi-fetch with types generated from `contracts/openapi.json`, `ApiError` + code → message mapping, central `queryKeys`, `shouldRetryQuery`, oxlint with `fetch` banned outside the API client, the design-token lint script, Tailwind v4 `@theme` tokens with the default palette removed, self-hosted fonts.
- Added lint bans: `sonner`, `@microsoft/signalr`, `openapi-fetch` outside `src/shared`.

## Consequences
+ One obvious home per concern; agents can't reach for the network or toast library from a component.
- Different paths from FlowOps; agents moving between the repos must read frontend-conventions.md.

## Alternatives rejected
Copying FlowOps' `lib/` layout (conflicts with the brief).
