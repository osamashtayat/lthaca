import { useId, useRef } from 'react'
import type { KeyboardEvent, MouseEvent } from 'react'
import { nextRating } from './stubRatingValue'

/** Half steps from 0.5 to 5 (ADR-0024): 0.5, 1, 1.5 … 5. */
const VALUES = Array.from({ length: 10 }, (_, i) => (i + 1) / 2)
const STUBS = [1, 2, 3, 4, 5] as const

type StubRatingProps = {
  /** Accessible name, e.g. "Your rating". */
  label: string
  /** 0.5–5 in half steps, or null when unrated. */
  value: number | null
  onChange: (value: number) => void
  disabled?: boolean
}

/**
 * The user's own rating: five ticket stubs, each fully pink, left-half pink, or off. Touch and mouse: every target is a
 * whole stub (never a half-width one); tapping stub n sets n, tapping it again sets n − 0.5. Keyboard and screen reader:
 * ten visually hidden native radios (0.5 … 5), so arrows move by 0.5 with one tab stop; Home / End jump to 0.5 / 5.
 */
export function StubRating({ label, value, onChange, disabled = false }: StubRatingProps) {
  const id = useId()
  const group = useRef<HTMLDivElement>(null)

  function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (disabled || (e.key !== 'Home' && e.key !== 'End')) return
    e.preventDefault()
    const target = e.key === 'Home' ? 0.5 : 5
    onChange(target)
    group.current?.querySelector<HTMLInputElement>(`input[value="${target}"]`)?.focus()
  }

  function onTap(e: MouseEvent<HTMLLabelElement>, n: number) {
    // The label would "click" radio n, which does nothing when it is already checked: decide here instead.
    e.preventDefault()
    if (!disabled) onChange(nextRating(value, n))
  }

  return (
    // The radios are the keyboard / screen-reader interface; Home and End are handled for the whole group.
    // oxlint-disable-next-line jsx-a11y/no-static-element-interactions
    <div ref={group} role="radiogroup" tabIndex={-1} aria-label={label} onKeyDown={onKeyDown} className="flex gap-1.5">
      {STUBS.map((n) => {
        const fill = value === null ? 'off' : value >= n ? 'full' : value === n - 0.5 ? 'half' : 'off'
        return (
          <div key={n} className="relative flex h-11 min-w-11 flex-1 items-center has-focus-visible:outline-3 has-focus-visible:outline-offset-2 has-focus-visible:outline-pink-text">
            {VALUES.filter((v) => v === n || v === n - 0.5).map((v) => (
              <input
                key={v}
                id={`${id}-${v}`}
                type="radio"
                name={id}
                value={v}
                aria-label={`${v} of 5 stubs`}
                checked={value === v}
                disabled={disabled}
                onChange={() => onChange(v)}
                className="sr-only"
              />
            ))}
            {/* The whole stub is one tap target; the label's `for` keeps it tied to the radio for pointer users' hit-testing. */}
            {/* oxlint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-noninteractive-element-interactions */}
            <label
              htmlFor={`${id}-${n}`}
              aria-hidden="true"
              onClick={(e) => onTap(e, n)}
              className={`flex h-9 w-full items-center justify-center rounded-md border-2 border-dashed border-ink font-mono text-sm font-medium text-ink ${
                fill === 'full' ? 'bg-pink' : fill === 'half' ? 'bg-linear-to-r from-pink from-50% to-cream to-50%' : 'bg-cream text-ink-muted'
              }`}
            >
              {n}
            </label>
          </div>
        )
      })}
    </div>
  )
}
