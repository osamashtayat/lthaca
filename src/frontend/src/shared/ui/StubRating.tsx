import { useId } from 'react'

const STUBS = [1, 2, 3, 4, 5] as const

type StubRatingProps = {
  /** Accessible name, e.g. "Your rating". */
  label: string
  /** 1–5, or null when unrated. */
  value: number | null
  onChange: (value: number) => void
  disabled?: boolean
}

/** The user's own rating: five ticket stubs, pink when on. Native radios: arrow keys select, one tab stop. */
export function StubRating({ label, value, onChange, disabled = false }: StubRatingProps) {
  const name = useId()
  return (
    <div role="radiogroup" aria-label={label} className="flex gap-1.5">
      {STUBS.map((n) => {
        const on = value !== null && n <= value
        return (
          <label key={n} className="flex h-11 min-w-11 flex-1 items-center">
            <input
              type="radio"
              name={name}
              value={n}
              aria-label={`${n} of 5`}
              checked={value === n}
              disabled={disabled}
              onChange={() => onChange(n)}
              className="peer sr-only"
            />
            <span
              aria-hidden="true"
              className={`flex h-9 w-full items-center justify-center rounded-md border-2 border-dashed border-ink font-mono text-sm font-medium peer-focus-visible:outline-3 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-pink-text ${on ? 'bg-pink text-ink' : 'bg-cream text-ink-muted'}`}
            >
              {n}
            </span>
          </label>
        )
      })}
    </div>
  )
}
