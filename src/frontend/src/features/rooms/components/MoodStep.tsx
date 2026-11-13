import { useEffect, useRef, useState } from 'react'
import { MoodPicker, toMoodInput, type MoodPickerValue } from '@/features/mood'
import { ApiError, toUserMessage } from '@/shared/api'
import { Button, LoadingState, MonoLabel, ToggleChip } from '@/shared/ui'
import { useSetRoomMood } from '../api/useSetRoomMood'
import { HARD_NO_OPTIONS, readyCount } from '../roomCode'
import type { Room } from '../types'

type HardNo = (typeof HARD_NO_OPTIONS)[number]['value']
const INITIAL: MoodPickerValue = { moods: [], maxRuntime: 'any', language: null, text: '' }

/**
 * "Group — your mood tonight": the member's moods, how long they can stay up, private hard nos and "I'm ready".
 * Same rules as the solo screen (ADR-0022), worded as "your vibe for tonight". The server decides what the text means:
 * `mood.unclear` shows under the field; `textIgnored` is reported to the caller through `onReady`.
 */
export function MoodStep({
  room,
  onReady,
  onSkip,
}: {
  room: Room
  onReady: (textIgnored: boolean) => void
  /** The host may open the lobby without answering (the server lets them start with members not ready). */
  onSkip?: () => void
}) {
  const save = useSetRoomMood(room.code)
  const [value, setValue] = useState<MoodPickerValue>(INITIAL)
  const [hardNos, setHardNos] = useState<HardNo[]>([])
  const textRef = useRef<HTMLTextAreaElement>(null)
  const unclear = save.error instanceof ApiError && save.error.code === 'mood.unclear'

  useEffect(() => {
    if (unclear && !save.isPending) textRef.current?.focus()
  }, [unclear, save.isPending, save.submittedAt])

  function submit() {
    const { moods, maxRuntime, text } = toMoodInput(value)
    save.mutate({ moods, maxRuntime, hardNos, text }, { onSuccess: (answer) => onReady(answer.textIgnored) })
  }

  function toggleHardNo(no: HardNo) {
    setHardNos((current) => (current.includes(no) ? current.filter((n) => n !== no) : [...current, no]))
  }

  return (
    <section className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6">
      <div className="flex flex-col gap-2">
        <MonoLabel>{room.name}</MonoLabel>
        <h1 className="text-4xl font-extrabold tracking-tight">Your vibe for tonight</h1>
        <p className="text-ink-muted">Tell us what you&apos;re in the mood for. The room combines everyone&apos;s answers.</p>
      </div>

      <MoodPicker
        value={value}
        onChange={setValue}
        textRef={textRef}
        textError={unclear ? toUserMessage(save.error) : undefined}
        showLanguage={false}
        runtimeLabel="How long can you stay up?"
        disabled={save.isPending}
      />

      <fieldset className="flex flex-col gap-2" disabled={save.isPending}>
        <legend className="mb-2 flex flex-col gap-1">
          <MonoLabel>Anything you&apos;d rather skip?</MonoLabel>
          <span className="text-sm text-ink-muted">Private: the group never sees these.</span>
        </legend>
        <div className="flex flex-wrap gap-2">
          {HARD_NO_OPTIONS.map((option) => (
            <ToggleChip key={option.value} selected={hardNos.includes(option.value)} onClick={() => toggleHardNo(option.value)}>
              {option.label}
            </ToggleChip>
          ))}
        </div>
      </fieldset>

      <p className="font-mono text-xs uppercase tracking-[0.12em] text-ink-muted">{readyCount(room.members)}</p>

      {save.isError && !unclear && (
        <p role="alert" className="font-bold text-pink-text">
          {toUserMessage(save.error)}
        </p>
      )}
      {save.isPending && <LoadingState label="Reading your vibe…" />}

      <Button size="lg" disabled={save.isPending} onClick={submit} className="w-full">
        {save.isPending ? 'Saving…' : "I'm ready"}
      </Button>
      {onSkip && (
        <Button variant="outline" onClick={onSkip} className="w-full">
          See who&apos;s here
        </Button>
      )}
    </section>
  )
}
