import { useState } from 'react'
import type { FormEvent } from 'react'
import { serverFieldError, toUserMessage } from '@/shared/api'
import { notify } from '@/shared/notify'
import { Button, Sheet, TextField } from '@/shared/ui'
import { useChangePassword } from '../api/useChangePassword'

function ChangePasswordForm({ onDone }: { onDone: () => void }) {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const change = useChangePassword()
  const currentError = serverFieldError(change.error, 'currentPassword')
  const newError = serverFieldError(change.error, 'newPassword')

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    change.mutate(
      { currentPassword, newPassword },
      { onSuccess: () => { notify.success('Password changed'); onDone() } },
    )
  }

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
      <p className="text-ink-muted">Other devices will be signed out. This one stays signed in.</p>
      <TextField
        label="Current password"
        type="password"
        value={currentPassword}
        onChange={(e) => setCurrentPassword(e.target.value)}
        autoComplete="current-password"
        error={currentError}
      />
      <TextField
        label="New password"
        type="password"
        value={newPassword}
        onChange={(e) => setNewPassword(e.target.value)}
        autoComplete="new-password"
        error={newError}
      />
      {change.isError && !currentError && !newError && (
        <p role="alert" className="font-bold text-pink-text">
          {toUserMessage(change.error)}
        </p>
      )}
      <Button type="submit" size="lg" disabled={change.isPending} className="w-full">
        Change password
      </Button>
    </form>
  )
}

/** "Change password": needs the current password (re-authentication, api-contract §14). Rebuilt on each open so no password lingers. */
export function ChangePasswordSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  return (
    <Sheet open={open} onClose={onClose} title="Change password">
      {open && <ChangePasswordForm onDone={onClose} />}
    </Sheet>
  )
}
