import { Navigate, Outlet, useLocation } from 'react-router'
import { ErrorState, LoadingState } from '@/shared/ui'
import { useCurrentUser } from '../api/useCurrentUser'
import { isUser } from '../types'

/**
 * Layout guard for user-only routes: renders the nested routes for a signed-in `User`, otherwise sends the visitor to
 * `/sign-in?next=<where they were going>`. A guest counts as signed out. The server still enforces access (401);
 * this only saves the round trip and the dead-end screen.
 */
export function RequireUser() {
  const session = useCurrentUser()
  const location = useLocation()

  if (session.isPending) return <LoadingState label="Checking your session…" />
  if (session.isError) return <ErrorState error={session.error} onRetry={() => void session.refetch()} />
  if (!isUser(session.data)) {
    const next = `${location.pathname}${location.search}`
    return <Navigate to={`/sign-in?next=${encodeURIComponent(next)}`} replace />
  }
  return <Outlet />
}
