import type { components } from '@/shared/api'

export type Mood = components['schemas']['Mood']
export type RuntimeLimit = components['schemas']['RuntimeLimit']
export type MoodInput = components['schemas']['MoodInput']

/** At most two mood chips (api-contract §5) and 280 characters of text. The server enforces both; this only stops the UI offering more. */
export const MAX_MOODS = 2
export const MAX_TEXT_LENGTH = 280

export const MOOD_OPTIONS: readonly { value: Mood; label: string }[] = [
  { value: 'cozy', label: 'Cozy' },
  { value: 'laugh', label: 'Make me laugh' },
  { value: 'keepGuessing', label: 'Keep me guessing' },
  { value: 'makeMeFeel', label: 'Make me feel' },
  { value: 'takeMeAway', label: 'Take me away' },
  { value: 'surprise', label: 'Surprise me' },
]

export const RUNTIME_OPTIONS: readonly { value: RuntimeLimit; label: string }[] = [
  { value: 'under90', label: 'Under 90' },
  { value: 'upTo2h', label: 'Up to 2h' },
  { value: 'any', label: 'Any' },
]

/** ISO 639-1 codes the picker offers; `null` is "any language". */
export const LANGUAGE_OPTIONS: readonly { value: string; label: string }[] = [
  { value: '', label: 'Any language' },
  { value: 'en', label: 'English' },
  { value: 'ar', label: 'Arabic' },
  { value: 'fr', label: 'French' },
  { value: 'es', label: 'Spanish' },
  { value: 'de', label: 'German' },
  { value: 'it', label: 'Italian' },
  { value: 'ja', label: 'Japanese' },
  { value: 'ko', label: 'Korean' },
]

export const EMPTY_MOOD_INPUT: MoodInput = { moods: [], maxRuntime: 'any', language: null, text: null }

/** Taps a chip: removes it if chosen, adds it unless two are already chosen (then the tap does nothing). */
export function toggleMood(moods: readonly Mood[], mood: Mood): Mood[] {
  if (moods.includes(mood)) return moods.filter((m) => m !== mood)
  return moods.length >= MAX_MOODS ? [...moods] : [...moods, mood]
}

/** The request body for the picker's state: blank text is sent as null so the server makes no LLM call for it. */
export function toMoodInput(state: { moods: readonly Mood[]; maxRuntime: RuntimeLimit; language: string | null; text: string }): MoodInput {
  const text = state.text.trim()
  return { moods: [...state.moods], maxRuntime: state.maxRuntime, language: state.language, text: text === '' ? null : text }
}
