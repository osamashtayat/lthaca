import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from '@/mocks/server'

// Every test talks to the mock API (ADR-0020). Unhandled requests fail the test: code must never reach the network
// unexpectedly, and a missing handler is a bug in the test or the mocks.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))

afterEach(() => {
  // Per-test overrides (server.use) end with the test. Mock stores add their reset calls here.
  server.resetHandlers()
})

afterAll(() => server.close())
