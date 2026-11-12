import { useId } from 'react'

type Option<T extends string> = { value: T; label: string }

type SegmentedControlProps<T extends string> = {
  /** Accessible name of the group. */
  label: string
  options: readonly Option<T>[]
  value: T
  onChange: (value: T) => void
}

/** Native radio group on a beige track: the browser provides arrow-key selection and the single tab stop. */
export function SegmentedControl<T extends string>({ label, options, value, onChange }: SegmentedControlProps<T>) {
  const name = useId()
  return (
    <div role="radiogroup" aria-label={label} className="flex rounded-full bg-beige p-1">
      {options.map((option) => (
        <label key={option.value} className="flex-1">
          <input
            type="radio"
            name={name}
            value={option.value}
            checked={option.value === value}
            onChange={() => onChange(option.value)}
            className="peer sr-only"
          />
          <span className="flex min-h-11 items-center justify-center rounded-full border-2 border-transparent px-4 text-sm font-extrabold text-ink-muted peer-checked:border-ink peer-checked:bg-yellow peer-checked:text-ink peer-focus-visible:outline-3 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-pink-text">
            {option.label}
          </span>
        </label>
      ))}
    </div>
  )
}
