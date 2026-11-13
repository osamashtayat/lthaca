import { useId } from 'react'
import type { ComponentProps } from 'react'

type TextFieldProps = Omit<ComponentProps<'input'>, 'id'> & {
  label: string
  /** Message shown under the field (a server field error). Marks the input invalid. */
  error?: string | undefined
}

/** Labelled text input with its error under it. Ink outline, 44px+ tall, errors announced via aria-describedby. */
export function TextField({ label, error, className = '', ...props }: TextFieldProps) {
  const id = useId()
  const errorId = `${id}-error`
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="font-mono text-xs uppercase tracking-[0.12em] text-ink-muted">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className={`min-h-12 rounded-control border-2 border-ink bg-cream px-4 text-base text-ink ${className}`}
        {...props}
      />
      {error && (
        <p id={errorId} className="text-sm font-bold text-pink-text">
          {error}
        </p>
      )}
    </div>
  )
}
