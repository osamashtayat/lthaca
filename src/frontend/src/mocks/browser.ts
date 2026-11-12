import { setupWorker } from 'msw/browser'
import { handlers } from './handlers'

/**
 * Starts the mock API in the browser: dev server only, loaded by a dynamic import in main.tsx, so production bundles
 * never contain it. Requests no handler covers (assets, fonts) go to the network untouched.
 */
export async function startMockApi() {
  const worker = setupWorker(...handlers)
  await worker.start({ onUnhandledFrame: 'bypass', quiet: true })
}
