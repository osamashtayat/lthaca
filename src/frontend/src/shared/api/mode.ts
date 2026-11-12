/**
 * The one home of the mock-API rules (ADR-0020).
 * Mocks answer on the dev server unless `VITE_API_MOCKS=off`, and always in Vitest (src/test/setup.ts starts MSW).
 * Production builds: `import.meta.env.DEV` is false, so both flags are fixed there (no mocks, realtime on).
 */
export const API_MOCKS_ENABLED: boolean =
  import.meta.env.DEV && (import.meta.env.MODE === 'test' || import.meta.env.VITE_API_MOCKS !== 'off')

/** No hub to talk to while mocks answer; with the real API, realtime is on. */
export const REALTIME_ENABLED: boolean = !API_MOCKS_ENABLED
