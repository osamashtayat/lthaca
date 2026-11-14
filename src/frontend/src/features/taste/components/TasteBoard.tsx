import { useNavigate } from 'react-router'
import { useCreateDeck } from '@/features/deck'
import { ApiError, toUserMessage } from '@/shared/api'
import { Button, LoadingState, MonoLabel, TicketCard } from '@/shared/ui'
import type { Taste } from '../types'

/** "Solo — by taste" when the server says the user is ready: the taste receipt and "Deal my deck". */
export function TasteBoard({ taste, onNotReady }: { taste: Taste; onNotReady: () => void }) {
  const navigate = useNavigate()
  const create = useCreateDeck()

  function deal() {
    create.mutate(
      { source: 'taste', mood: null },
      {
        onSuccess: (deck) => void navigate(`/deck/${deck.id}`),
        // The server changed its mind (e.g. a film was un-liked meanwhile): re-read readiness, which shows the import screen.
        onError: (error) => error instanceof ApiError && error.code === 'taste.not_ready' && onNotReady(),
      },
    )
  }

  return (
    <section className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-4xl font-extrabold tracking-tight">By taste</h1>
        <p className="text-ink-muted">A deck built from the films you&apos;ve loved.</p>
      </div>

      <TicketCard stubLabel="YOUR TASTE" stubValue={String(taste.loggedCount)}>
        <div className="flex flex-col gap-4">
          <p className="font-bold">From {taste.loggedCount} films you&apos;ve logged</p>
          {taste.topGenres.length > 0 && (
            <ul className="flex flex-col gap-2" aria-label="Your top genres">
              {taste.topGenres.map((g) => (
                <li key={g.genre} className="flex flex-col gap-1">
                  <span className="flex justify-between font-mono text-xs tracking-[0.12em] uppercase">
                    <span>{g.genre}</span>
                    <span>{g.percent}%</span>
                  </span>
                  <span aria-hidden="true" className="h-3 overflow-hidden rounded-full border-2 border-ink bg-cream">
                    <span className="block h-full bg-pink" style={{ width: `${g.percent}%` }} />
                  </span>
                </li>
              ))}
            </ul>
          )}
          {taste.notForYou.length > 0 && <MonoLabel>Not for you: {taste.notForYou.join(', ')}</MonoLabel>}
        </div>
      </TicketCard>

      {create.isError && !(create.error instanceof ApiError && create.error.code === 'taste.not_ready') && (
        <p role="alert" className="font-bold text-pink-text">
          {toUserMessage(create.error)}
        </p>
      )}
      {create.isPending && <LoadingState label="Dealing your deck…" />}

      <Button size="lg" disabled={create.isPending} onClick={deal} className="w-full">
        {create.isPending ? 'Dealing…' : 'Deal my deck'}
      </Button>
    </section>
  )
}
