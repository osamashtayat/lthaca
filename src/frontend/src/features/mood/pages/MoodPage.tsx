import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { useCreateDeck } from '@/features/deck'
import { ApiError, toUserMessage } from '@/shared/api'
import { Button, LoadingState } from '@/shared/ui'
import { MoodPicker, type MoodPickerValue } from '../components/MoodPicker'
import { toMoodInput } from '../moodState'

const INITIAL: MoodPickerValue = { moods: [], maxRuntime: 'any', language: null, text: '' }

/**
 * "Solo — by mood": the picker and "Deal my deck". The server turns the words into filters (and decides what to do
 * with text that isn't a mood, ADR-0022); this screen only maps its answer to the right place: `mood.unclear` under
 * the text field with the text kept and focused, every other failure as one message above the button. Messages come
 * from problem.ts and never repeat what the user typed.
 */
export function MoodPage() {
  const navigate = useNavigate()
  const create = useCreateDeck()
  const [value, setValue] = useState<MoodPickerValue>(INITIAL)
  const textRef = useRef<HTMLTextAreaElement>(null)

  function deal() {
    create.mutate(
      { source: 'mood', mood: toMoodInput(value) },
      { onSuccess: (deck) => void navigate(`/deck/${deck.id}`) },
    )
  }

  const unclear = create.error instanceof ApiError && create.error.code === 'mood.unclear'

  // The text is kept; put the cursor back in it (it was disabled while the request ran, so wait for the re-render).
  useEffect(() => {
    if (unclear && !create.isPending) textRef.current?.focus()
  }, [unclear, create.isPending, create.submittedAt])

  return (
    <section className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-4xl font-extrabold tracking-tight">By mood</h1>
        <p className="text-ink-muted">Tell us the mood and we&apos;ll deal a deck to match.</p>
      </div>

      <MoodPicker
        value={value}
        onChange={setValue}
        textRef={textRef}
        textError={unclear ? toUserMessage(create.error) : undefined}
        disabled={create.isPending}
      />

      {create.isError && !unclear && (
        <p role="alert" className="font-bold text-pink-text">
          {toUserMessage(create.error)}
        </p>
      )}
      {create.isPending && <LoadingState label="Reading your mood…" />}

      <Button size="lg" disabled={create.isPending} onClick={deal} className="w-full">
        {create.isPending ? 'Dealing…' : 'Deal my deck'}
      </Button>
    </section>
  )
}
