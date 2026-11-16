import { browserTracingIntegration } from '@sentry/react'

/**
 * Tracing, loaded after the app is idle so it stays out of the first download on phones; error capture starts at once
 * without it. Page loads and navigations are named by route (`/rooms/:code`), never by URL, so room codes, ids and
 * queries stay out of trace names. Session Replay is left out on purpose: its recorder stores full page URLs (reset
 * tokens, search text) in events the SDK can't scrub (ADR-0028, FE-21).
 */
export function lazyIntegrations(nameRoute: (pathname: string) => string) {
  return [
    browserTracingIntegration({
      beforeStartSpan: (options) => ({
        ...options,
        name: nameRoute(window.location.pathname),
        attributes: { ...options.attributes, 'sentry.source': 'route' },
      }),
    }),
  ]
}
