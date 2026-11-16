import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router'
import { serverFieldError, toUserMessage } from '@/shared/api'
import { Button, TextField, TicketCard } from '@/shared/ui'
import { useForgotPassword } from '../api/useForgotPassword'

/** Asks for an email and always answers the same way: we never say whether it has an account. */
export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const forgot = useForgotPassword()
  const error = forgot.error
  const emailError = serverFieldError(error, 'email')

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    forgot.mutate({ email })
  }

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-4xl font-extrabold tracking-tight">Forgot your password?</h1>
        <p className="text-ink-muted">Enter your email and we'll send you a link to choose a new one.</p>
      </div>

      <TicketCard stubLabel="RESET" stubValue="✉">
        {forgot.isSuccess ? (
          <output className="flex flex-col gap-4">
            <p className="font-bold">Check your email</p>
            <p className="text-ink-muted">
              If an account exists for that address, a reset link is on its way. It works for an hour and only once.
            </p>
            <Link to="/sign-in" className="inline-flex min-h-11 items-center font-bold text-ink underline">
              Back to sign in
            </Link>
          </output>
        ) : (
          <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
            <TextField
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
              error={emailError}
            />
            {error && !emailError && (
              <p role="alert" className="font-bold text-pink-text">
                {toUserMessage(error)}
              </p>
            )}
            <Button type="submit" size="lg" disabled={forgot.isPending} className="w-full">
              Send reset link
            </Button>
            <Link to="/sign-in" className="inline-flex min-h-11 items-center justify-center font-bold text-ink underline">
              Back to sign in
            </Link>
          </form>
        )}
      </TicketCard>
    </div>
  )
}
