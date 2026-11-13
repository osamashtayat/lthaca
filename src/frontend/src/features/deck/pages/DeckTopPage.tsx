import { Link, useParams } from 'react-router'
import { ApiError } from '@/shared/api'
import { ErrorState, LoadingState, MonoLabel, Poster, TicketCard, tmdbImageUrl } from '@/shared/ui'
import { useDeckTop } from '../api/useDeckTop'

/** "Solo — your top 3": the deck's best right-swiped films, with the server's reason when it has one. */
export function DeckTopPage() {
  const deckId = String(useParams().deckId)
  const top = useDeckTop(deckId)

  if (top.isPending) return <LoadingState label="Picking your top films…" />
  if (top.isError) {
    if (top.error instanceof ApiError && top.error.code === 'deck.no_yeses') {
      return (
        <section className="mx-auto flex max-w-sm flex-col items-start gap-4">
          <h1 className="text-3xl font-extrabold tracking-tight">No yeses yet</h1>
          <ErrorState error={top.error} />
          <Link
            to={`/deck/${deckId}`}
            className="inline-flex min-h-11 items-center rounded-full border-2 border-ink bg-yellow px-5 font-extrabold text-ink no-underline"
          >
            Back to the deck
          </Link>
        </section>
      )
    }
    return <ErrorState error={top.error} onRetry={() => void top.refetch()} />
  }

  return (
    <section className="mx-auto flex w-full max-w-md flex-col gap-5">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-extrabold tracking-tight">Your top {top.data.picks.length}</h1>
        <MonoLabel>
          from {top.data.swipeCount} swipes · {top.data.yesCount} yeses
        </MonoLabel>
      </div>

      <ol className="flex flex-col gap-5">
        {top.data.picks.map(({ film, reason }, index) => (
          <li key={film.tmdbId}>
            <TicketCard stubLabel={`PICK ${index + 1}`} stubValue={String(index + 1)}>
              <div className="flex gap-4">
                <Poster src={tmdbImageUrl(film.posterPath, 'w342')} alt={`${film.title} poster`} className="w-24 shrink-0" />
                <div className="flex min-w-0 flex-col gap-1">
                  <h2 className="text-xl leading-tight font-extrabold tracking-tight">
                    <Link to={`/films/${film.tmdbId}`} className="text-ink no-underline">
                      {film.title}
                    </Link>
                  </h2>
                  <p className="font-mono text-xs text-ink-muted">
                    {[film.releaseYear, film.runtimeMinutes ? `${film.runtimeMinutes} min` : null].filter(Boolean).join(' · ')}
                  </p>
                  {reason && <p className="text-sm">{reason}</p>}
                </div>
              </div>
            </TicketCard>
          </li>
        ))}
      </ol>

      <div className="flex flex-wrap gap-3">
        <Link
          to={`/deck/${deckId}`}
          className="inline-flex min-h-11 items-center rounded-full border-2 border-ink bg-yellow px-5 font-extrabold text-ink no-underline"
        >
          Keep swiping
        </Link>
        <Link to="/deck" className="inline-flex min-h-11 items-center px-2 font-bold text-pink-text">
          New deck
        </Link>
      </div>
    </section>
  )
}
