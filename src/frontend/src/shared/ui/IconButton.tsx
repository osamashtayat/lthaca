import type { ComponentProps, ReactNode } from 'react'

type IconButtonProps = Omit<ComponentProps<'button'>, 'aria-label' | 'children'> & {
  /** Required: an icon alone has no accessible name. */
  'aria-label': string
  /** The icon (decorative SVG). */
  children: ReactNode
}

/** 44px square icon-only button. */
export function IconButton({ type = 'button', className = '', ...props }: IconButtonProps) {
  return (
    <button
      type={type}
      className={`flex size-11 shrink-0 items-center justify-center rounded-control border-2 border-ink bg-yellow text-ink disabled:opacity-50 ${className}`}
      {...props}
    />
  )
}
