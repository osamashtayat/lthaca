import { useState } from 'react'
import type { FormEvent } from 'react'
import { serverFieldError, toUserMessage } from '@/shared/api'
import { notify } from '@/shared/notify'
import { Button, TextField } from '@/shared/ui'
import { useUpdateProfile } from '../api/useUpdateProfile'

/** Edit the display name in place. Trimming and the 1–40 rule are the server's: its field error shows under the input. */
export function DisplayNameForm({ displayName }: { displayName: string }) {
  const [value, setValue] = useState(displayName)
  const update = useUpdateProfile()
  const fieldError = serverFieldError(update.error, 'displayName')

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    update.mutate(value, { onSuccess: (profile) => { setValue(profile.displayName); notify.success('Name saved') } })
  }

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-3">
      <TextField label="Name" value={value} onChange={(e) => setValue(e.target.value)} autoComplete="nickname" error={fieldError} />
      {update.isError && !fieldError && (
        <p role="alert" className="font-bold text-pink-text">
          {toUserMessage(update.error)}
        </p>
      )}
      <Button type="submit" variant="outline" disabled={update.isPending || value === displayName} className="self-start">
        Save name
      </Button>
    </form>
  )
}
