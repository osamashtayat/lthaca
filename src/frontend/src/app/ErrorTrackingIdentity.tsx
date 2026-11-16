import { useEffect } from 'react'
import { useCurrentUser } from '@/features/auth'
import { setErrorTrackingUser } from '@/shared/monitoring'

/** Tells Sentry who is signed in, by internal id and kind only (ADR-0028). Renders nothing. */
export function ErrorTrackingIdentity() {
  const { data: user } = useCurrentUser()
  useEffect(() => {
    if (user === undefined) return
    setErrorTrackingUser(user ? { id: user.id, kind: user.kind } : null)
  }, [user])
  return null
}
