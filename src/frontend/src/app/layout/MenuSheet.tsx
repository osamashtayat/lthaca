import { useEffect, useRef } from 'react'
import { NavLink } from 'react-router'
import { CloseIcon, Wordmark } from '@/shared/ui'
import { NAV_ITEMS } from './navigation'

/**
 * The ☰ menu: a full-screen native modal <dialog> on phones. The browser handles the focus trap and Escape;
 * the close button or any link closes it.
 */
export function MenuSheet({ onClose }: { onClose: () => void }) {
  const dialogRef = useRef<HTMLDialogElement>(null)

  // No cleanup: close() would fire onClose (StrictMode re-runs effects); unmounting removes the dialog anyway.
  useEffect(() => {
    const dialog = dialogRef.current
    if (dialog && !dialog.open) dialog.showModal()
  }, [])

  return (
    <dialog
      ref={dialogRef}
      id="main-menu"
      aria-label="Menu"
      onClose={onClose}
      className="m-0 h-dvh max-h-none w-full max-w-none bg-cream p-0 text-ink lg:hidden"
    >
      <div className="flex h-(--header-height) items-center border-b-2 border-ink pr-4 pl-5">
        <Wordmark className="flex-1 text-[26px]" />
        <button
          type="button"
          onClick={onClose}
          aria-label="Close menu"
          className="flex size-11 items-center justify-center rounded-control border-2 border-ink bg-yellow text-ink"
        >
          <CloseIcon />
        </button>
      </div>
      <nav aria-label="Main" className="mx-5 mt-4 flex flex-col">
        {NAV_ITEMS.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/'}
            onClick={onClose}
            className="group flex h-[62px] items-center gap-3.5 border-b-[1.5px] border-line text-ink no-underline"
          >
            <span className="h-[26px] w-2.5 rounded-sm group-aria-[current=page]:bg-yellow" aria-hidden="true" />
            <span className="flex-1 text-[26px] font-extrabold tracking-tight">{item.label}</span>
            <span className="font-mono text-xs text-ink-muted">{item.meta}</span>
          </NavLink>
        ))}
      </nav>
    </dialog>
  )
}
