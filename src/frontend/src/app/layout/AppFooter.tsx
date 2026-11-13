import tmdbLogo from '@/shared/ui/assets/tmdb-logo.svg'

/**
 * TMDB attribution (API Terms of Use §3, ADR-0017), on every page: the official TMDB logo, unmodified, kept small so it
 * is less prominent than the Ithaca wordmark, plus the required notice. The logo is an image, not a link: a 12px link
 * would be a touch target under 44px.
 */
export function AppFooter() {
  return (
    <footer className="mx-auto flex w-full max-w-(--content-max-width) flex-col items-start gap-2 px-5 pt-10 pb-8 font-mono text-xs text-ink-muted lg:px-8">
      <img src={tmdbLogo} alt="TMDB" width={92} height={12} className="h-3 w-auto" />
      <p>This website uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.</p>
    </footer>
  )
}
