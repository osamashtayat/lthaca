import { addIntegration, captureException, init, reactErrorHandler, setTag, setUser } from '@sentry/react'
import { createBrowserRouter, matchRoutes, type RouteObject } from 'react-router'

/**
 * Sentry for the SPA (ADR-0028): unhandled errors, React render errors and failed route loads, plus page-load and
 * navigation traces. Off without `VITE_SENTRY_DSN` and always off in Vitest, so tests and the mock API never send
 * anything. Users are reported by internal id only. No Session Replay (see lazyIntegrations.ts).
 */

const dsn = import.meta.env.VITE_SENTRY_DSN ?? ''

/** True when events are sent: a DSN is set and this isn't a test run. */
export const ERROR_TRACKING_ENABLED = dsn !== '' && import.meta.env.MODE !== 'test'

/** Starts Sentry. Must run before the router is created (src/shared/monitoring/init.ts, imported first by main.tsx). */
export function initErrorTracking() {
  if (!ERROR_TRACKING_ENABLED) return
  const production = import.meta.env.PROD
  init({
    dsn,
    environment: production ? 'production' : 'development',
    release: import.meta.env.VITE_SENTRY_RELEASE || undefined,
    // Collect nothing personal: no user info, cookies, bodies or query params; only the User-Agent header.
    dataCollection: {
      userInfo: false,
      cookies: false,
      httpHeaders: { request: { allow: ['User-Agent'] }, response: false },
      httpBodies: [],
      urlQueryParams: false,
    },
    // Tracing arrives with addLazyIntegrations(); errors are captured from the first line.
    tracesSampleRate: production ? 0.1 : 1.0,
    // Same origin only: the API gets the trace headers, nothing else does.
    tracePropagationTargets: [/^\/api\//],
    beforeSend: scrubEvent,
    // Spans are streamed (SDK v11 default), so they are scrubbed here, not in beforeSendTransaction.
    beforeSendSpan: scrubSpan,
    beforeBreadcrumb: scrubBreadcrumb,
  })
  addLazyIntegrations()
}

/** Loads tracing in its own chunk once the browser is idle, then adds it to the running client. */
function addLazyIntegrations() {
  const load = () =>
    void import('./lazyIntegrations')
      .then(({ lazyIntegrations }) => lazyIntegrations(nameRoute).forEach(addIntegration))
      .catch(() => {}) // a failed chunk load only loses traces; errors are still captured
  if ('requestIdleCallback' in window) window.requestIdleCallback(load, { timeout: 5000 })
  else setTimeout(load, 2000)
}

/** Who is signed in, as the internal id only (never email or name); null when signed out. */
export function setErrorTrackingUser(user: { id: string; kind: string } | null) {
  if (!ERROR_TRACKING_ENABLED) return
  setUser(user ? { id: user.id } : null)
  setTag('actor.kind', user?.kind ?? 'anonymous')
}

/**
 * Reports an error React never saw: the router handles loader, action and lazy-chunk failures itself. Render errors are
 * already reported by `reactErrorHandlers`; reporting the same error object again is deduplicated by the SDK.
 */
export function reportError(error: unknown) {
  if (ERROR_TRACKING_ENABLED) captureException(error)
}

/**
 * `createRoot` options so React reports every error it catches or can't catch. Empty when Sentry is off, so React keeps
 * its own console reporting in development and tests.
 */
export const reactErrorHandlers = ERROR_TRACKING_ENABLED
  ? {
      onUncaughtError: reactErrorHandler(),
      onCaughtError: reactErrorHandler(),
      onRecoverableError: reactErrorHandler(),
    }
  : {}

let routeTable: RouteObject[] = []

/** `createBrowserRouter` that also lets Sentry name page loads and navigations by route (`/rooms/:code`), never by URL. */
export function createTracedBrowserRouter(routes: RouteObject[]) {
  routeTable = routes
  return createBrowserRouter(routes)
}

/** The route pattern a path matches (`/rooms/ABC123/swipe` → `/rooms/:code/swipe`); `/*` when none does. */
export function nameRoute(pathname: string, routes: RouteObject[] = routeTable): string {
  const pattern = (matchRoutes(routes, pathname) ?? [])
    .map((match) => match.route.path ?? '')
    .filter((path) => path !== '')
    .join('/')
    .replace(/\/+/g, '/')
  return pattern === '' ? '/' : pattern.startsWith('/') ? pattern : `/${pattern}`
}

/** Removes the query string and fragment from every absolute or root-relative URL in `text` (search text, reset tokens). */
export function withoutQueries(text: string): string {
  return text.replace(/((?:https?:\/\/|\/)[^\s?#"']*)[?#][^\s"']*/g, '$1')
}

function scrubUrls(value: unknown): unknown {
  if (typeof value === 'string') return withoutQueries(value)
  if (Array.isArray(value)) return value.map(scrubUrls)
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([key, inner]) => [key, scrubUrls(inner)]))
  }
  return value
}

type ScrubbableEvent = {
  request?: { url?: string; query_string?: unknown; cookies?: unknown; headers?: Record<string, string>; data?: unknown }
  user?: { id?: string | number; ip_address?: string | null; [key: string]: unknown }
}

/** Drops query strings, cookies, bodies and user fields other than the id before anything leaves the browser. */
export function scrubEvent<T extends ScrubbableEvent>(event: T): T {
  if (event.request) {
    const { headers } = event.request
    event.request = {
      url: event.request.url ? withoutQueries(event.request.url) : undefined,
      headers: headers?.['User-Agent'] ? { 'User-Agent': headers['User-Agent'] } : undefined,
    }
  }
  // ip_address null (not missing): tells Sentry not to infer the IP, and with it a city-level location.
  event.user = event.user?.id === undefined ? { ip_address: null } : { id: event.user.id, ip_address: null }
  return event
}

type ScrubbableSpan = { name: string; attributes: Record<string, unknown> }

/** A span keeps its name and attributes without query strings (fetch URLs, page URLs). */
export function scrubSpan<T extends ScrubbableSpan>(span: T): T {
  span.name = withoutQueries(span.name)
  span.attributes = scrubUrls(span.attributes) as T['attributes']
  return span
}

type ScrubbableBreadcrumb = { message?: string; data?: Record<string, unknown> }

/** Fetch, navigation and console breadcrumbs keep their URLs without queries (`/reset-password?token=…`). */
export function scrubBreadcrumb<T extends ScrubbableBreadcrumb>(breadcrumb: T): T {
  if (breadcrumb.message) breadcrumb.message = withoutQueries(breadcrumb.message)
  if (breadcrumb.data) breadcrumb.data = scrubUrls(breadcrumb.data) as Record<string, unknown>
  return breadcrumb
}
