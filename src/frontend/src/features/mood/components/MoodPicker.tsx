import type { Ref } from 'react'
import { MonoLabel, SegmentedControl, TextArea, ToggleChip } from '@/shared/ui'
import { LANGUAGE_OPTIONS, MAX_MOODS, MAX_TEXT_LENGTH, MOOD_OPTIONS, RUNTIME_OPTIONS, toggleMood, type Mood, type RuntimeLimit } from '../moodState'

export type MoodPickerValue = { moods: Mood[]; maxRuntime: RuntimeLimit; language: string | null; text: string }

type MoodPickerProps = {
  value: MoodPickerValue
  onChange: (value: MoodPickerValue) => void
  /** Message under the text field (ADR-0022 `mood.unclear`). */
  textError?: string | undefined
  /** The text field, so the screen can focus it after an error. */
  textRef?: Ref<HTMLTextAreaElement>
  /** Rooms (FE-10) have no language filter. */
  showLanguage?: boolean
  disabled?: boolean
}

/**
 * The shared mood controls: up to two mood chips, "How long have you got?", language and "Describe it your way".
 * Controlled and free of requests, so the solo screen and the room mood step (FE-10) both use it.
 */
export function MoodPicker({ value, onChange, textError, textRef, showLanguage = true, disabled = false }: MoodPickerProps) {
  const full = value.moods.length >= MAX_MOODS

  return (
    <div className="flex flex-col gap-6">
      <fieldset className="flex flex-col gap-2" disabled={disabled}>
        <legend className="mb-2">
          <MonoLabel>Pick up to {MAX_MOODS} moods</MonoLabel>
        </legend>
        <div className="flex flex-wrap gap-2">
          {MOOD_OPTIONS.map((option) => {
            const selected = value.moods.includes(option.value)
            return (
              <ToggleChip
                key={option.value}
                selected={selected}
                // A third chip is not offered; the chosen two stay tappable to swap.
                aria-disabled={!selected && full ? true : undefined}
                onClick={() => onChange({ ...value, moods: toggleMood(value.moods, option.value) })}
                className={!selected && full ? 'opacity-50' : ''}
              >
                {option.label}
              </ToggleChip>
            )
          })}
        </div>
      </fieldset>

      <div className="flex flex-col gap-2">
        <MonoLabel>How long have you got?</MonoLabel>
        <SegmentedControl
          label="How long have you got?"
          options={RUNTIME_OPTIONS}
          value={value.maxRuntime}
          onChange={(maxRuntime) => onChange({ ...value, maxRuntime })}
        />
      </div>

      {showLanguage && (
        <div className="flex flex-col gap-1.5">
          <label htmlFor="mood-language" className="font-mono text-xs uppercase tracking-[0.12em] text-ink-muted">
            Language
          </label>
          <select
            id="mood-language"
            disabled={disabled}
            value={value.language ?? ''}
            onChange={(e) => onChange({ ...value, language: e.target.value === '' ? null : e.target.value })}
            className="min-h-12 rounded-control border-2 border-ink bg-cream px-4 text-base text-ink"
          >
            {LANGUAGE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </div>
      )}

      <TextArea
        ref={textRef}
        label="Describe it your way"
        placeholder="funny and under 2 hours…"
        maxLength={MAX_TEXT_LENGTH}
        disabled={disabled}
        value={value.text}
        onChange={(e) => onChange({ ...value, text: e.target.value })}
        error={textError}
        counter={`${value.text.length} / ${MAX_TEXT_LENGTH}`}
      />
    </div>
  )
}
