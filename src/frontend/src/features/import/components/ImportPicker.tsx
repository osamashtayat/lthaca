import { useId, useRef } from 'react'
import type { ChangeEvent } from 'react'
import { toUserMessage } from '@/shared/api'
import { LetterboxdHelpSheet } from './LetterboxdHelpSheet'
import { useCreateImport } from '../api/useCreateImport'
import type { ImportJob } from '../types'

/**
 * "Choose your Letterboxd ZIP": the native file picker (a ZIP, or one CSV from it), the "?" steps, and the upload's
 * error right below. The file goes straight to the upload; its contents are never read in JavaScript.
 */
export function ImportPicker({ onCreated }: { onCreated: (job: ImportJob) => void }) {
  const create = useCreateImport()
  const input = useRef<HTMLInputElement>(null)
  const errorId = useId()

  function onPick(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    create.mutate(file, { onSuccess: onCreated })
    e.target.value = '' // picking the same file again must fire `change` again
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <label className="inline-flex min-h-[58px] flex-1 cursor-pointer items-center justify-center rounded-full border-2 border-ink bg-ink px-8 text-lg font-extrabold tracking-tight text-cream has-focus-visible:outline-3 has-focus-visible:outline-offset-2 has-focus-visible:outline-pink-text">
          {create.isPending ? 'Uploading…' : 'Choose your Letterboxd ZIP'}
          <input
            ref={input}
            type="file"
            accept=".zip,.csv"
            disabled={create.isPending}
            onChange={onPick}
            aria-describedby={create.isError ? errorId : undefined}
            className="sr-only"
          />
        </label>
        <LetterboxdHelpSheet />
      </div>
      {create.isError && (
        <p id={errorId} role="alert" className="font-bold text-pink-text">
          {toUserMessage(create.error)}
        </p>
      )}
    </div>
  )
}
