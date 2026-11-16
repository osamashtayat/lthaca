import { useEffect } from 'react'
import { isRouteErrorResponse, useRouteError } from 'react-router'
import { reportError } from '@/shared/monitoring'
import { CrashPage } from './CrashPage'
import { NotFoundPage } from './NotFoundPage'

/**
 * The router's error element: a thrown 404 is "not found", anything else is a crash. Render errors are reported by
 * React's handlers; loader and lazy-chunk failures never reach React, so they are reported here (deduplicated).
 */
export function RouteErrorPage() {
  const error = useRouteError()
  const notFound = isRouteErrorResponse(error) && error.status === 404
  useEffect(() => {
    if (!notFound) reportError(error)
  }, [error, notFound])
  if (notFound) {
    return (
      <main className="mx-auto w-full max-w-(--content-max-width) px-5 py-6">
        <NotFoundPage />
      </main>
    )
  }
  return <CrashPage />
}
