import { useState } from 'react'
import { useWatchlist } from '@/features/library'
import { BookmarkIcon } from '@/shared/ui'

/** Saves the film to the watchlist, separate from swiping (a right swipe is only a deck "yes"). Shown at once, undone on failure. */
export function WatchlistButton({ tmdbId, initial, title }: { tmdbId: number; initial: boolean; title: string }) {
  const [saved, setSaved] = useState(initial)
  const watchlist = useWatchlist()

  function toggle() {
    const on = !saved
    setSaved(on)
    watchlist.mutate({ tmdbId, on }, { onError: () => setSaved(!on) })
  }

  return (
    <button
      type="button"
      aria-pressed={saved}
      aria-label={`Save ${title} to your watchlist`}
      onClick={toggle}
      className={`flex size-11 items-center justify-center rounded-full border-2 border-ink text-ink ${saved ? 'bg-yellow shadow-hard-yellow' : 'bg-cream'}`}
    >
      <BookmarkIcon filled={saved} />
    </button>
  )
}
