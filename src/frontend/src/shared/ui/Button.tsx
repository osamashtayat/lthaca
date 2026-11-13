import type { ComponentProps } from 'react'
import { Link } from 'react-router'

export type ButtonVariant = 'ink' | 'yellow' | 'pink' | 'outline'

const variants: Record<ButtonVariant, string> = {
  ink: 'bg-ink text-cream',
  yellow: 'bg-yellow text-ink',
  pink: 'bg-pink text-ink',
  outline: 'bg-transparent text-ink',
}

type ButtonSize = 'md' | 'lg'

/** The pill look shared by `Button` and `ButtonLink`. `lg` is the big bottom CTA (58px). */
function pillClasses(variant: ButtonVariant, size: ButtonSize, className: string) {
  const height = size === 'lg' ? 'min-h-[58px] px-8 text-lg' : 'min-h-11 px-5'
  return `inline-flex items-center justify-center gap-2 rounded-full border-2 border-ink font-extrabold tracking-tight no-underline ${height} ${variants[variant]} ${className}`
}

type ButtonProps = ComponentProps<'button'> & {
  variant?: ButtonVariant
  size?: ButtonSize
}

/** Pill button. Defaults to type="button" so it never submits a form by accident. */
export function Button({ variant = 'ink', size = 'md', type = 'button', className = '', ...props }: ButtonProps) {
  return <button type={type} className={`${pillClasses(variant, size, className)} disabled:opacity-50`} {...props} />
}

/** A router link that looks like a `Button`: for navigation, where a real `<a>` is the right element. */
export function ButtonLink({
  variant = 'ink',
  size = 'md',
  className = '',
  ...props
}: ComponentProps<typeof Link> & { variant?: ButtonVariant; size?: ButtonSize }) {
  return <Link className={pillClasses(variant, size, className)} {...props} />
}
