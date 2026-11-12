import type { ComponentProps } from 'react'

const base = 'inline-flex min-h-8 items-center rounded-full border-2 border-ink px-3.5 text-sm font-bold'

/** A static tag (genre, year). Not interactive: use ToggleChip for a choice. */
export function Chip({ className = '', ...props }: ComponentProps<'span'>) {
  return <span className={`${base} bg-cream ${className}`} {...props} />
}

type ToggleChipProps = Omit<ComponentProps<'button'>, 'aria-pressed'> & { selected: boolean }

/** A pill toggle. Selected: ink with a yellow hard shadow. The 44px target comes from the padding box, not the look. */
export function ToggleChip({ selected, type = 'button', className = '', ...props }: ToggleChipProps) {
  return (
    <button
      type={type}
      aria-pressed={selected}
      className={`${base} min-h-11 ${selected ? 'bg-ink text-cream shadow-hard-yellow' : 'bg-cream text-ink'} ${className}`}
      {...props}
    />
  )
}
