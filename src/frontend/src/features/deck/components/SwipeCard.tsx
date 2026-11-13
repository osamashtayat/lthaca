/* oxlint-disable jsx-a11y/no-noninteractive-element-interactions, jsx-a11y/no-noninteractive-tabindex --
   the card is a keyboard-operable group (arrow keys swipe; the same actions are real buttons beside it). */
import { useEffect, useRef } from 'react'
import type { KeyboardEvent, ReactNode } from 'react'
import { Poster, tmdbImageUrl } from '@/shared/ui'
import type { Film } from '@/features/films'
import { useSwipeGesture } from '../useSwipeGesture'
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
 * The swipeable film card: drag it right / left / up, or focus it and press → ← ↑. Reduced motion: no tilt and no
 * spring-back animation. Presentation only; what a swipe means is the caller's.
 */
export function SwipeCard({ film, onSwipe, allowed = ['right', 'left', 'up'], disabled = false, corner }: SwipeCardProps) {
  const { handlers, offset, dragging, pending } = useSwipeGesture({ onSwipe, disabled, allowed })
  const ref = useRef<HTMLElement>(null)

  // After a swipe the previous card unmounts and focus falls to <body>: hand it to the next card, so arrow keys keep
  // working. Focus that is somewhere else on purpose (a button, a field) is left alone.
  useEffect(() => {
    if (!disabled && (document.activeElement === document.body || document.activeElement === null)) {
      ref.current?.focus({ preventScroll: true })
    }
  }, [disabled])

  function onKeyDown(e: KeyboardEvent<HTMLElement>) {
    if (disabled) return
    const direction = ({ ArrowRight: 'right', ArrowLeft: 'left', ArrowUp: 'up' } as const)[e.key as 'ArrowRight']
    if (!direction || !allowed.includes(direction)) return
    e.preventDefault()
    onSwipe(direction)
  }

  const facts = [film.releaseYear, film.runtimeMinutes ? `${film.runtimeMinutes} min` : null].filter(Boolean).join(' · ')

  return (
    <article
      ref={ref}
      aria-label={`${film.title}. Arrow keys: right to watch, left to skip, up if you've seen it.`}
      tabIndex={disabled ? -1 : 0}
      onKeyDown={onKeyDown}
      {...handlers}
      style={{ transform: `translate(${offset.x}px, ${offset.y}px) rotate(${offset.x / 20}deg)` }}
      className={`relative w-full touch-none select-none overflow-hidden rounded-card border-[2.5px] border-ink bg-cream shadow-hard motion-reduce:transform-none! ${dragging ? '' : 'transition-transform duration-200 motion-reduce:transition-none'}`}
    >
      <Poster src={tmdbImageUrl(film.posterPath, 'w500')} alt={`${film.title} poster`} className="rounded-none border-0" />
      <div className="absolute inset-x-0 bottom-0 flex flex-col gap-1 bg-ink/85 p-4 pl-16 text-cream">
        <h2 className="text-2xl leading-tight font-extrabold tracking-tight">{film.title}</h2>
        <p className="font-mono text-xs tracking-[0.12em] text-cream-soft uppercase">
          {[facts, film.genres.slice(0, 2).join(' / ')].filter(Boolean).join(' · ')}
        </p>
      </div>
      {pending && allowed.includes(pending) && (
        <span
          aria-hidden="true"
          className={`absolute rounded-full border-2 border-ink px-3 py-1 font-mono text-xs font-medium tracking-[0.12em] text-ink uppercase ${HINTS[pending].place}`}
        >
          {HINTS[pending].label}
        </span>
      )}
      {corner && (
        // A press here is a button press, not the start of a drag.
        <div className="absolute bottom-3 left-3" onPointerDown={(e) => e.stopPropagation()}>
          {corner}
        </div>
      )}
    </article>
  )
}
