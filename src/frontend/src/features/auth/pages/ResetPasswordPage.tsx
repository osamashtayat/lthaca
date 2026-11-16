import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { serverFieldError, toUserMessage } from '@/shared/api'
import { Button, TextField, TicketCard } from '@/shared/ui'
import { useResetPassword } from '../api/useResetPassword'

/** The emailed link's page: `/reset-password?token=…`. The token is opaque; the server decides whether it still works. */
export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const navigate = useNavigate()
  const [newPassword, setNewPassword] = useState('')
  const reset = useResetPassword()
  const error = reset.error
  const passwordError = serverFieldError(error, 'newPassword')

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    reset.mutate({ token, newPassword }, { onSuccess: () => void navigate('/', { replace: true }) })
  }

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-4xl font-extrabold tracking-tight">Choose a new password</h1>
        <p className="text-ink-muted">You'll be signed in once it's saved.</p>
      </div>

      <TicketCard stubLabel="RESET" stubValue="★">
        {token === '' ? (
          <div role="alert" className="flex flex-col gap-4">
            <p className="font-bold text-pink-text">This reset link is incomplete. Open the link from your email again, or ask for a new one.</p>
            <Link to="/forgot-password" className="inline-flex min-h-11 items-center font-bold text-ink underline">
              Ask for a new link
            </Link>
          </div>
        ) : (
          <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
            <TextField
              label="New password"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              autoComplete="new-password"
              error={passwordError}
            />
            {error && !passwordError && (
              <div role="alert" className="flex flex-col gap-2">
                <p className="font-bold text-pink-text">{toUserMessage(error)}</p>
                <Link to="/forgot-password" className="inline-flex min-h-11 items-center font-bold text-ink underline">
                  Ask for a new link
                </Link>
              </div>
            )}
            <Button type="submit" size="lg" disabled={reset.isPending} className="w-full">
              Save password
            </Button>
          </form>
        )}
      </TicketCard>
    </div>
  )
}
