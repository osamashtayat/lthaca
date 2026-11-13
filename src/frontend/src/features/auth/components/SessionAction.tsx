import { Link } from 'react-router'
import { notify } from '@/shared/notify'
import { Button } from '@/shared/ui'
import { useCurrentUser } from '../api/useCurrentUser'
import { useSignOut } from '../api/useSignOut'
import { isUser } from '../types'

/** "Sign in" for visitors, "Sign out" (with the name) for signed-in users. Used in the ☰ menu and the desktop header. */
export function SessionAction({ onNavigate, className = '' }: { onNavigate?: () => void; className?: string }) {
  const session = useCurrentUser()
  const signOut = useSignOut()

  if (session.isPending || session.isError) return null

  if (!isUser(session.data)) {
    return (
      <Link
        to="/sign-in"
        onClick={onNavigate}
        className={`inline-flex min-h-11 items-center justify-center rounded-full border-2 border-ink bg-yellow px-5 font-extrabold text-ink no-underline ${className}`}
      >
        Sign in
      </Link>
    )
  }

  return (
    <Button
      variant="outline"
      className={className}
      disabled={signOut.isPending}
      onClick={() => {
        signOut.mutate(undefined, {
          onSuccess: onNavigate,
          onError: (error) => notify.apiError(error),
        })
      }}
    >
      Sign out · {session.data.displayName}
    </Button>
  )
}
