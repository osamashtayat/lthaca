import type { ComponentProps } from 'react'

export type ButtonVariant = 'ink' | 'yellow' | 'pink' | 'outline'

const variants: Record<ButtonVariant, string> = {
  ink: 'bg-ink text-cream',
  yellow: 'bg-yellow text-ink',
  pink: 'bg-pink text-ink',
  outline: 'bg-transparent text-ink',
}

type ButtonProps = ComponentProps<'button'> & {
  variant?: ButtonVariant
  /** `lg` is the big bottom CTA (58px). */
  size?: 'md' | 'lg'
}

/** Pill button. Defaults to type="button" so it never submits a form by accident. */
export function Button({ variant = 'ink', size = 'md', type = 'button', className = '', ...props }: ButtonProps) {
  const height = size === 'lg' ? 'min-h-[58px] px-8 text-lg' : 'min-h-11 px-5'
  return (
    <button
      type={type}
      className={`inline-flex items-center justify-center gap-2 rounded-full border-2 border-ink font-extrabold tracking-tight disabled:opacity-50 ${height} ${variants[variant]} ${className}`}
      {...props}
    />
  )
}
