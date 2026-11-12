# ADR-0020: Mock API with MSW for the dev server and Vitest (supersedes the MSW part of ADR-0010)

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
ADR-0010 left MSW out of the skeleton until a feature needed it. Now a backend agent and a frontend agent work in
parallel (ADR-0021): the frontend must build every screen before the backend logic exists, and tests must never reach the
network. FlowOps solved this with MSW (`src/mocks/`, a `main.tsx` toggle, a node server in Vitest).

## Decision
- **MSW 3** in `src/frontend/src/mocks/`: `handlers.ts` (one block per contract area), `http.ts` (`apiPath`,
  `ResponseOf`/`RequestOf` typed from the generated `schema.d.ts`, `problem()` restricted to codes in `problem.ts`),
  `fixtures.ts`, `browser.ts` (worker), `server.ts` (node), `MockApiFailedBanner.tsx`.
- **One flag file:** `shared/api/mode.ts` holds `API_MOCKS_ENABLED` and `REALTIME_ENABLED`.
- **Dev server: mocks on by default**, `VITE_API_MOCKS=off` switches to the real API; `main.tsx` starts the worker from
  `API_MOCKS_ENABLED` through a dynamic import.
- **Production never contains mocks:** the dynamic import sits behind `import.meta.env.DEV`; a Vite build plugin deletes
  `mockServiceWorker.js` from `dist/`; `scripts/check-no-mocks.mjs` runs as part of `npm run build` (so also in CI and
  the Docker image) and fails if any mock code is in the bundle.
- **Vitest** uses the same handlers through `setupServer` (`src/test/setup.ts`), with `onUnhandledFrame: 'error'`:
  a request without a handler fails the test. Tests override per case with `server.use(...)`.
- **Realtime off with mocks:** `REALTIME_ENABLED` (`shared/realtime`) is false while mocks answer or in tests;
  `useRoomHub` never connects then (FlowOps' `REALTIME_ENABLED`). Screens test event handling by calling handlers directly.
- **Lint:** `fetch` is allowed in `src/mocks/**` (e.g. `fetch(bypass(request))` to pass a request through) besides
  `src/shared/api/**`; `msw` may be imported only in `src/mocks/**` and `src/test/**`.
- **Only contract paths are mocked.** `apiPath()` accepts generated paths only, so the frontend can't mock an endpoint
  that isn't in `contracts/openapi.json` (ADR-0021).

### Deviation from FlowOps
FlowOps' browser worker mocks only `pendingHandlers` (endpoints whose backend task isn't merged) and lets the rest reach
the real API. Ithaca's dev server mocks **everything** when on, so the frontend agent never needs a running backend or
database; integration is a separate, explicit step (`VITE_API_MOCKS=off`) when the matching backend task is done.

## Consequences
+ Frontend tasks never wait for backend logic; tests are offline and deterministic.
+ A contract change breaks mocks at compile time (`ResponseOf`), not silently at runtime.
- Mocks are a second, simplified implementation of server behaviour; keep them dumb (state + contract shapes) and never
  put business rules in them that screens would then depend on.
- Two browsers don't share mock state; multi-user room flows are checked against the real API.

## Alternatives rejected
`pendingHandlers` only (FlowOps; needs the backend running for every screen); `openapi-msw` (another dependency for what
`ResponseOf` does in a few lines); a JSON-server or Prism mock from openapi.json (no stateful flows, extra process).
