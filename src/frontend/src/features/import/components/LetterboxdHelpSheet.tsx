import { useRef, useState } from 'react'
import { IconButton, Sheet } from '@/shared/ui'

const STEPS = [
  "Letterboxd's export works only on its website in a desktop browser view, not in the Letterboxd app.",
  'On your phone: open letterboxd.com in Safari or Chrome, switch to the desktop site (Safari: the page settings menu, "Request Desktop Website"; Chrome: ⋮ menu, "Desktop site"), and sign in. Or use a computer.',
  'Go to Settings, then Data, then "Export your data" (direct link: letterboxd.com/user/exportdata/) and tap Export Data. A ZIP file downloads.',
  'Come back to Ithaca, tap "Choose your Letterboxd ZIP" and pick the file (Files or Downloads).',
] as const

/**
 * The "?" next to anything that needs the Letterboxd export (FE-13's empty state, FE-14's `/import`): a 44px button
 * that opens the steps in a bottom sheet and gives focus back to itself when the sheet closes. The steps live here once.
 */
export function LetterboxdHelpSheet() {
  const [open, setOpen] = useState(false)
  const trigger = useRef<HTMLButtonElement>(null)

  function close() {
    setOpen(false)
    trigger.current?.focus()
  }

  return (
    <>
      <IconButton ref={trigger} aria-label="How to get your Letterboxd file" aria-haspopup="dialog" onClick={() => setOpen(true)}>
        <span aria-hidden="true" className="text-lg font-extrabold">
          ?
        </span>
      </IconButton>
      <Sheet open={open} onClose={close} title="Get your Letterboxd file">
        <ol className="flex list-decimal flex-col gap-3 pl-5">
          {STEPS.map((step) => (
            <li key={step}>{step}</li>
          ))}
        </ol>
        <p className="mt-4 text-ink-muted">We only read your watched films, ratings, likes and watchlist. We never keep the file.</p>
      </Sheet>
    </>
  )
}
