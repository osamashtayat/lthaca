/**
 * TMDB attribution, required by the TMDB API terms on every page that shows TMDB data (ADR-0017).
 * TODO(attribution): add the TMDB logo (less prominent than the Ithaca wordmark) once the asset is approved.
 */
export function AppFooter() {
  return (
    <footer className="mx-auto w-full max-w-(--content-max-width) px-5 pt-10 pb-8 font-mono text-xs text-ink-muted lg:px-8">
      This website uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.
    </footer>
  )
}
