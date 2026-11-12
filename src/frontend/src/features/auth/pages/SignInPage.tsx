import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate, useSearchParams } from 'react-router'
import { serverFieldError, toUserMessage } from '@/shared/api'
import { Button, LoadingState, SegmentedControl, TextField, TicketCard } from '@/shared/ui'
import { useCurrentUser } from '../api/useCurrentUser'
import { useRegister } from '../api/useRegister'
import { useSignIn } from '../api/useSignIn'
import { RoomCodeForm } from '../components/RoomCodeForm'
import { safeNextPath } from '../nextPath'
import { isUser } from '../types'

type Mode = 'sign-in' | 'register'
const MODES = [
  { value: 'sign-in', label: 'Sign in' },
  { value: 'register', label: 'Create account' },
] as const

/** The Welcome board: email sign-in or account creation, plus "Have a room code?". Validation is the server's. */
export function SignInPage() {
  const [params] = useSearchParams()
  const next = safeNextPath(params.get('next'))
  const navigate = useNavigate()
  const session = useCurrentUser()

  const [mode, setMode] = useState<Mode>('sign-in')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const signIn = useSignIn()
  const register = useRegister()
  const action = mode === 'sign-in' ? signIn : register

  if (session.isPending) return <LoadingState label="Loading…" />
  if (isUser(session.data)) return <Navigate to={next} replace />

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    const onSuccess = () => void navigate(next, { replace: true })
    if (mode === 'sign-in') signIn.mutate({ email, password }, { onSuccess })
    else register.mutate({ email, password, displayName }, { onSuccess })
  }

  const error = action.error
  const hasFieldErrors = ['email', 'password', 'displayName'].some((f) => serverFieldError(error, f))

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-4xl font-extrabold tracking-tight">Welcome</h1>
        <p className="text-ink-muted">Sign in to keep your picks, or create an account. No account? Join a room below.</p>
      </div>

      <TicketCard stubLabel="ADMIT 1" stubValue={mode === 'sign-in' ? '→' : '+'}>
        <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <SegmentedControl
            label="Sign in or create an account"
            options={MODES}
            value={mode}
            onChange={(value) => {
              setMode(value)
              signIn.reset()
              register.reset()
            }}
          />
          {mode === 'register' && (
            <TextField
              label="Name"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              autoComplete="nickname"
              error={serverFieldError(error, 'displayName')}
            />
          )}
          <TextField
            label="Email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            error={serverFieldError(error, 'email')}
          />
          <TextField
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete={mode === 'sign-in' ? 'current-password' : 'new-password'}
            error={serverFieldError(error, 'password')}
          />
          {error && !hasFieldErrors && (
            <p role="alert" className="font-bold text-pink-text">
              {toUserMessage(error)}
            </p>
          )}
          <Button type="submit" size="lg" disabled={action.isPending} className="w-full">
            {mode === 'sign-in' ? 'Sign in' : 'Create account'}
          </Button>
        </form>
      </TicketCard>

      <RoomCodeForm />
    </div>
  )
}
