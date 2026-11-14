import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { serverFieldError, toUserMessage } from '@/shared/api'
import { Button, Sheet, TextField } from '@/shared/ui'
import { useDeleteAccount } from '../api/useDeleteAccount'

function DeleteAccountForm() {
  const [password, setPassword] = useState('')
  const navigate = useNavigate()
  const remove = useDeleteAccount()
  const fieldError = serverFieldError(remove.error, 'password')

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    remove.mutate(password, { onSuccess: () => void navigate('/', { replace: true }) })
  }

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
      <p className="font-bold">This can&apos;t be undone.</p>
      <p className="text-ink-muted">
        Your account, My Cinema, decks, taste, imports and room votes are deleted. Rooms you host that are still open are closed.
        You can sign up again with the same email.
      </p>
      <TextField
        label="Your password"
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        autoComplete="current-password"
        error={fieldError}
      />
      {remove.isError && !fieldError && (
        <p role="alert" className="font-bold text-pink-text">
          {toUserMessage(remove.error)}
        </p>
      )}
      <Button type="submit" variant="pink" size="lg" disabled={remove.isPending} className="w-full">
        Delete my account
      </Button>
    </form>
  )
}

/** "Delete account": asks for the password again, then the server removes everything and signs the caller out. */
export function DeleteAccountSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  return (
    <Sheet open={open} onClose={onClose} title="Delete account">
      {open && <DeleteAccountForm />}
    </Sheet>
  )
}
