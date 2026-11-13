import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { BookmarkIcon, CheckIcon, CloseIcon, ErrorState, HeartIcon, LoadingState, MonoLabel } from '@/shared/ui'
import type { Film } from '@/features/films'
import { useDeck } from '../api/useDeck'
import { useMoreItems } from '../api/useMoreItems'
import { useSwipe } from '../api/useSwipe'
import { SwipeCard } from '../components/SwipeCard'
import { VerdictSheet } from '../components/VerdictSheet'
import { WatchlistButton } from '../components/WatchlistButton'
import { pendingItems } from '../deckCache'
import type { DeckItem, SeenVerdict, SwipeDirection } from '../types'

/** Ask for the next batch when this few cards are left. */
const PREFETCH_AT = 3

/** "Solo — the deck": the card stack, the three buttons, the "seen it" sheet and the way to the top 3. */
export function DeckPage() {
  const deckId = String(useParams().deckId)
  const deck = useDeck(deckId)
  const more = useMoreItems(deckId)
  const { swipe } = useSwipe(deckId)
  const [seenFilm, setSeenFilm] = useState<DeckItem | null>(null)
  const [exhausted, setExhausted] = useState(false)

  const pending = deck.data ? pendingItems(deck.data) : []
  const fetchMore = more.mutate

  // Prefetch the next batch near the end. A failed fetch is not retried in a loop: the card still on screen can be
  // swiped, and an empty batch means the deck is finished.
  useEffect(() => {
    if (!deck.isSuccess || exhausted || more.isPending || more.isError || pending.length > PREFETCH_AT) return
    fetchMore(undefined, { onSuccess: (items) => items.length === 0 && setExhausted(true) })
  }, [deck.isSuccess, exhausted, more.isPending, more.isError, pending.length, fetchMore])

  if (deck.isPending) return <LoadingState label="Loading your deck…" />
  if (deck.isError) return <ErrorState error={deck.error} onRetry={() => void deck.refetch()} />

  const current = pending[0]
  const next = pending[1]

  function onSwipe(item: DeckItem, direction: SwipeDirection) {
    if (direction === 'up') setSeenFilm(item)
    else swipe(item.id, direction)
  }
  function chooseVerdict(verdict: SeenVerdict | null) {
    if (seenFilm) swipe(seenFilm.id, 'up', verdict)
    setSeenFilm(null)
  }
  const film: Film | undefined = current?.film

  return (
    <section className="mx-auto flex w-full max-w-sm flex-col gap-5 pb-4">
      <div className="flex items-center justify-between">
        <MonoLabel>
          {deck.data.swipeCount} swipes · {deck.data.yesCount} yeses
        </MonoLabel>
        {deck.data.yesCount > 0 && (
          <Link to={`/deck/${deckId}/top`} className="inline-flex min-h-11 items-center font-bold text-pink-text">
            See top 3
          </Link>
        )}
      </div>

      {deck.data.mood?.textIgnored && deck.data.swipeCount === 0 && (
        // ADR-0022: a quiet note, not a toast; it goes away with the first swipe. Never repeats the user's text.
        <p className="font-mono text-xs tracking-[0.12em] text-ink-muted">
          We went with your moods; we couldn&apos;t make sense of the text.
        </p>
      )}

      {current && film ? (
        <>
          <div className="relative">
            {next && (
              <div aria-hidden="true" className="absolute inset-x-3 top-3 -z-10 h-full rounded-card border-[2.5px] border-ink bg-beige" />
            )}
            <SwipeCard
              key={current.id}
              film={film}
              onSwipe={(direction) => onSwipe(current, direction)}
              corner={<WatchlistButton key={current.id} tmdbId={film.tmdbId} initial={current.onWatchlist} title={film.title} />}
            />
          </div>

          <div className="flex items-center justify-center gap-5">
            <RoundButton label="Not for me" onClick={() => onSwipe(current, 'left')}>
              <CloseIcon />
            </RoundButton>
            <RoundButton label="Seen it" onClick={() => onSwipe(current, 'up')}>
              <CheckIcon />
            </RoundButton>
            <RoundButton label="I'd watch it" primary onClick={() => onSwipe(current, 'right')}>
              <HeartIcon />
            </RoundButton>
          </div>
          <p className="text-center font-mono text-xs tracking-[0.12em] text-ink-muted uppercase">
            <BookmarkIcon size={12} /> saves it for later · swipe or use the buttons
          </p>
        </>
      ) : more.isPending ? (
        <LoadingState label="Dealing more cards…" />
      ) : (
        <div className="flex flex-col items-start gap-4">
          <h1 className="text-3xl font-extrabold tracking-tight">{exhausted ? "That's the whole deck" : 'No more cards right now'}</h1>
          {deck.data.yesCount > 0 ? (
            <Link
              to={`/deck/${deckId}/top`}
              className="inline-flex min-h-[58px] items-center rounded-full border-2 border-ink bg-ink px-8 text-lg font-extrabold text-cream no-underline"
            >
              See your top 3
            </Link>
          ) : (
            <p className="text-ink-muted">You didn't say yes to anything. Start a new deck to try again.</p>
          )}
          <Link to="/deck" className="inline-flex min-h-11 items-center font-bold text-pink-text">
            New deck
          </Link>
        </div>
      )}

      <VerdictSheet film={seenFilm?.film ?? null} onChoose={chooseVerdict} onClose={() => setSeenFilm(null)} />
    </section>
  )
}

function RoundButton({
  label,
  primary = false,
  onClick,
  children,
}: {
  label: string
  primary?: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={onClick}
      className={`flex size-16 items-center justify-center rounded-full border-2 border-ink text-ink shadow-hard ${primary ? 'bg-pink' : 'bg-cream'}`}
    >
      {children}
    </button>
  )
}
