import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from '@/mocks/server'

// jsdom (used by component tests, opted in per file) doesn't implement the modal <dialog> API; this minimal stand-in
// sets `open` and fires `close` like the browser does. It does not model focus trapping.
if (typeof HTMLDialogElement !== 'undefined' && !HTMLDialogElement.prototype.showModal) {
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '')
  }
  HTMLDialogElement.prototype.close = function close() {
    if (!this.open) return
    this.removeAttribute('open')
    this.dispatchEvent(new Event('close'))
  }
}

// Every test talks to the mock API (ADR-0020). Unhandled requests fail the test: code must never reach the network
// unexpectedly, and a missing handler is a bug in the test or the mocks.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))

afterEach(() => {
  // Per-test overrides (server.use) end with the test. Mock stores add their reset calls here.
  server.resetHandlers()
})

afterAll(() => server.close())
