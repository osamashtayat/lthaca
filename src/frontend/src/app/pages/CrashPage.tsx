import { Button } from '@/shared/ui'

/**
 * Shown instead of a blank page when the app crashes (ADR-0028). Works outside the router too, so it links with a plain
 * `<a>`. Says what happened and what to do; the error itself has already gone to Sentry.
 */
export function CrashPage() {
  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-(--content-max-width) flex-col items-start justify-center gap-4 px-5 py-6">
      <h1 className="font-display text-4xl">Well, that's a plot twist</h1>
      <p>Something broke on this screen. We've been told about it, so reloading usually gets you back on track.</p>
      <div className="flex flex-wrap items-center gap-3">
        <Button onClick={() => window.location.reload()}>Reload</Button>
        <a href="/" className="inline-flex min-h-11 items-center font-bold text-pink-text">
          Back to tonight
        </a>
      </div>
    </main>
  )
}
