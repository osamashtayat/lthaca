import { ErrorBoundary } from '@sentry/react'
import type { ReactNode } from 'react'

/**
 * Catches a render error outside the router (providers, the router itself) and shows `fallback` instead of a blank page.
 * Errors inside routes are caught by the route's `errorElement`. Reporting happens in React's error handlers.
 */
export function CrashBoundary({ fallback, children }: { fallback: ReactNode; children: ReactNode }) {
  return <ErrorBoundary fallback={<>{fallback}</>}>{children}</ErrorBoundary>
}
