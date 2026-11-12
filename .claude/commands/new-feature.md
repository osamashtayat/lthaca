---
description: Build a frontend feature page the Ithaca way (feature folder, api/ hook, route)
argument-hint: <feature> <PageName> [route] [design canvas screen]
---

Build `$ARGUMENTS` in the Ithaca frontend. Follow AGENTS.md §2–3 and docs/frontend-conventions.md; copy the shape of
`src/frontend/src/features/films/` (the reference slice).

Steps, in order:
1. Check the endpoint exists in `contracts/openapi.json`. If not, stop and run `/new-endpoint` first (don't mock it in the page).
2. Run `npm run api:types` if the contract changed. Add types in `features/<f>/types.ts` as aliases of `components['schemas'][…]`.
3. Add query keys to `src/shared/query/queryKeys.ts` (one block per feature).
4. Add hooks in `features/<f>/api/` (`useX` for queries, `useXMutation` for writes; mutations invalidate by key prefix and report
   failures with `notify.apiError(error)`).
5. Build the page in `features/<f>/pages/<PageName>.tsx` and pieces in `components/`. Pages read params, call hooks, render
   loading/error/empty/success. Use `LoadingState`/`ErrorState` and the tokens (`tokens.css`); match the design canvas screen.
   Mobile first (375px), touch targets ≥ 44px, real `<button>`/`<a>`, visible focus.
6. Export the page from `features/<f>/index.ts` and swap the matching `PlaceholderPage` in `src/app/routes.tsx`.
7. Real-time screens: `useRoomHub({ handlers })` with handlers that invalidate queries; never parse event names by hand.
8. Verify: `npm run typecheck && npm run lint && npm test && npm run build`; open the page at phone width.
9. Run the `architecture-reviewer` agent, fix "Must fix" items, then finish with `/report`.

Don't: call `fetch`, import `sonner`/`@microsoft/signalr`/`openapi-fetch`, hard-code colours, or decide server rules in the UI.
