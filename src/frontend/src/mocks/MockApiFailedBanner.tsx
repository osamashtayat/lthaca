/**
 * Dev server only (see main.tsx): shown when the mock API could not start, e.g. a browser that blocks service workers.
 * Without it, every screen would fail with a generic message and the cause would hide in the console.
 */
export function MockApiFailedBanner() {
  return (
    <p role="alert" className="fixed inset-x-0 top-0 z-50 border-b-2 border-ink bg-yellow px-4 py-2 text-center text-sm text-ink">
      Dev only: the mock API could not start (service workers blocked?). Requests go to the real API. Try another browser,
      or see the console.
    </p>
  )
}
