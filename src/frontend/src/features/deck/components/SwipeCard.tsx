/* oxlint-disable jsx-a11y/no-noninteractive-element-interactions, jsx-a11y/no-noninteractive-tabindex --
   the card is a keyboard-operable group (arrow keys swipe; the same actions are real buttons beside it). */
import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent, ReactNode } from 'react'
import { Poster, tmdbImageUrl } from '@/shared/ui'
import type { Film } from '@/features/films'
import { filmFacts } from '../filmFacts'
import { useSwipeGesture } from '../useSwipeGesture'
import { FilmStorySheet } from './FilmStorySheet'
import type { SwipeDirection } from '../types'

const HINTS: Record<SwipeDirection, { label: string; place: string }> = {
  right: { label: "I'd watch it", place: 'top-4 left-4 -rotate-6 bg-yellow' },
  left: { label: 'Not for me', place: 'top-4 right-4 rotate-6 bg-cream' },
  up: { label: 'Seen it', place: 'top-4 left-1/2 -translate-x-1/2 bg-pink' },
}

type SwipeCardProps = {
  film: Film
  onSwipe: (direction: SwipeDirection) => void
  /** Which swipes this deck accepts (the group deck has no "seen it"). Defaults to all three. */
  allowed?: readonly SwipeDirection[]
  disabled?: boolean
  /** Overlaid at the bottom-left, outside the swipe (the watchlist button). Pointer presses on it never start a drag. */
  corner?: ReactNode
}

/**
 * The swipeable film card, as the deck board draws it: the poster, a ticket tear line, then the facts, the title and the
 * start of the story (FE-22). "More" opens the whole story in a sheet without leaving the deck. Drag it right / left /
 * up, or focus it and press → ← ↑. Reduced motion: no tilt and no spring-back animation. Presentation only; what a
 * swipe means is the caller's.
 */
export function SwipeCard({ film, onSwipe, allowed = ['right', 'left', 'up'], disabled = false, corner }: SwipeCardProps) {
  const { handlers, offset, dragging, pending } = useSwipeGesture({ onSwipe, disabled, allowed })
  const ref = useRef<HTMLElement>(null)
  const [storyOpen, setStoryOpen] = useState(false)

  // After a swipe the previous card unmounts and focus falls to <body>: hand it to the next card, so arrow keys keep
  // working. Focus that is somewhere else on purpose (a button, a field) is left alone.
  useEffect(() => {
    if (!disabled && (document.activeElement === document.body || document.activeElement === null)) {
      ref.current?.focus({ preventScroll: true })
    }
  }, [disabled])

  function onKeyDown(e: KeyboardEvent<HTMLElement>) {
    if (disabled || storyOpen) return // reading the story is not a swipe
    const direction = ({ ArrowRight: 'right', ArrowLeft: 'left', ArrowUp: 'up' } as const)[e.key as 'ArrowRight']
    if (!direction || !allowed.includes(direction)) return
    e.preventDefault()
    onSwipe(direction)
  }

  return (
    <>
      <article
        ref={ref}
        aria-label={`${film.title}. Arrow keys: right to watch, left to skip, up if you've seen it.`}
        tabIndex={disabled ? -1 : 0}
        onKeyDown={onKeyDown}
        {...handlers}
        style={{ transform: `translate(${offset.x}px, ${offset.y}px) rotate(${offset.x / 20}deg)` }}
        className={`relative w-full touch-none select-none overflow-hidden rounded-card border-[2.5px] border-ink bg-cream shadow-hard motion-reduce:transform-none! ${dragging ? '' : 'transition-transform duration-200 motion-reduce:transition-none'}`}
      >
        <div className="relative aspect-[6/7] border-b-[2.5px] border-dashed border-ink">
          <Poster
            src={tmdbImageUrl(film.posterPath, 'w500')}
            alt={`${film.title} poster`}
            className="size-full aspect-auto! rounded-none! border-0!"
          />
          {/* Ticket notches where the tear line meets the edges (the card clips each to a half circle). */}
          <span aria-hidden="true" className="absolute -bottom-3 -left-3 size-6 rounded-full border-[2.5px] border-ink bg-cream" />
          <span aria-hidden="true" className="absolute -right-3 -bottom-3 size-6 rounded-full border-[2.5px] border-ink bg-cream" />
          {corner && (
            // A press here is a button press, not the start of a drag.
            <div className="absolute bottom-3 left-3" onPointerDown={(e) => e.stopPropagation()}>
              {corner}
            </div>
          )}
        </div>
        {/* Fixed height, so the card never jumps with the length of the title or the story. */}
        <div className="flex h-38 flex-col gap-0.5 px-4 pt-2.5 pb-3">
          <p className="truncate font-mono text-xs tracking-[0.06em] uppercase">{filmFacts(film)}</p>
          <div className="flex items-center gap-2">
            <h2 className="min-w-0 flex-1 truncate text-2xl leading-tight font-extrabold tracking-tight">{film.title}</h2>
            <button
              type="button"
              aria-label={`The story of ${film.title}`}
              onPointerDown={(e) => e.stopPropagation()}
              onClick={() => setStoryOpen(true)}
              className="inline-flex min-h-11 shrink-0 items-center rounded-full border-2 border-ink px-4 text-sm font-bold"
            >
              More
            </button>
          </div>
          {film.overview && <p className="line-clamp-3 text-sm leading-snug">{film.overview}</p>}
        </div>
        {pending && allowed.includes(pending) && (
          <span
            aria-hidden="true"
            className={`absolute rounded-full border-2 border-ink px-3 py-1 font-mono text-xs font-medium tracking-[0.12em] text-ink uppercase ${HINTS[pending].place}`}
          >
            {HINTS[pending].label}
          </span>
        )}
      </article>
      {/* Outside the card: keys and pointers in the sheet must never reach the card's swipe handlers. */}
      <FilmStorySheet film={film} open={storyOpen} onClose={() => setStoryOpen(false)} />
    </>
  )
}
