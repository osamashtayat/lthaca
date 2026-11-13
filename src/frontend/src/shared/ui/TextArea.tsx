import { useId } from 'react'
import type { ComponentProps } from 'react'

type TextAreaProps = Omit<ComponentProps<'textarea'>, 'id'> & {
  label: string
  /** Message shown under the field. Marks the field invalid. */
  error?: string | undefined
  /** Shown right-aligned under the field, e.g. "12 / 280". */
  counter?: string
}

/** Labelled multi-line input, the sibling of TextField: ink outline, error under it, announced via aria-describedby. */
export function TextArea({ label, error, counter, className = '', ...props }: TextAreaProps) {
  const id = useId()
  const errorId = `${id}-error`
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="font-mono text-xs uppercase tracking-[0.12em] text-ink-muted">
        {label}
      </label>
      <textarea
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className={`min-h-24 resize-none rounded-control border-2 border-ink bg-cream px-4 py-3 text-base text-ink ${className}`}
        {...props}
      />
      <div className="flex justify-between gap-3">
        {error ? (
          <p id={errorId} className="text-sm font-bold text-pink-text">
            {error}
          </p>
        ) : (
          <span />
        )}
        {counter && <span className="shrink-0 font-mono text-xs whitespace-nowrap text-ink-muted">{counter}</span>}
      </div>
    </div>
  )
}
