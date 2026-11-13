import type { ComponentProps } from 'react'

/** DM Mono, uppercase, letter-spaced: "JUST SWIPE", "2 OF 3 READY". */
export function MonoLabel({ className = '', ...props }: ComponentProps<'span'>) {
  return <span className={`font-mono text-xs uppercase tracking-[0.12em] text-ink-muted ${className}`} {...props} />
}
