/* oxlint-disable jsx-a11y/click-events-have-key-events, jsx-a11y/no-noninteractive-element-interactions --
   backdrop click is a pointer-only convenience; keyboard users close with Escape (native) or the Close button. */
import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import { CloseIcon } from './icons'
import { IconButton } from './IconButton'

type SheetProps = {
  open: boolean
  onClose: () => void
  /** Heading and accessible name. */
  title: string
  children: ReactNode
}

/**
 * Bottom sheet on a native modal <dialog>: the browser traps focus, makes the page inert, closes on Escape and
 * returns focus to the opener. Backdrop click and the close button also call onClose.
 */
export function Sheet({ open, onClose, title, children }: SheetProps) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      aria-label={title}
      onClose={onClose}
      onClick={(e) => {
        if (e.target === ref.current) onClose() // a click on the ::backdrop targets the dialog itself
      }}
      className="m-0 mt-auto max-h-[90dvh] w-full max-w-none rounded-t-hero border-2 border-b-0 border-ink bg-cream p-0 text-ink backdrop:bg-ink/60 md:mx-auto md:max-w-lg"
    >
      <div className="flex items-center justify-between gap-3 px-5 pt-5">
        <h2 className="text-2xl font-extrabold tracking-tight">{title}</h2>
        <IconButton aria-label="Close" onClick={onClose}>
          <CloseIcon />
        </IconButton>
      </div>
      <div className="px-5 pt-4 pb-6">{children}</div>
    </dialog>
  )
}
