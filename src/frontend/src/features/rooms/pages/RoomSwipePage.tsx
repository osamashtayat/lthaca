import { Link, Navigate, useNavigate, useParams } from 'react-router'
import { useCurrentUser } from '@/features/auth'
import { SwipeButtons, SwipeCard } from '@/features/deck'
import { ApiError, toUserMessage } from '@/shared/api'
import { notify } from '@/shared/notify'
import { Button, ErrorState, LoadingState, MonoLabel } from '@/shared/ui'
import { useCastVote } from '../api/useCastVote'
import { useFinishRoom } from '../api/useFinishRoom'
import { useRoom } from '../api/useRoom'
import { useRoomDeck } from '../api/useRoomDeck'
import { useRoomLive } from '../api/useRoomLive'
import { useRoomProgress } from '../api/useRoomProgress'
import { normalizeRoomCode } from '../roomCode'

/**
 * "Group — swipe": the shared deck. Right = I'd watch it, left = not for me, up = I've seen it (the result shows who
 * has). The server decides the match; this screen only follows the room's status, live: a match or a finish moves
 * everyone to the result, a close ends it here. "4 OF 12" is the caller's own count; "LIVE · N SWIPING" is how many
 * members are still going.
 */
export function RoomSwipePage() {
  const { code = '' } = useParams()
  const key = normalizeRoomCode(code)
  const navigate = useNavigate()
  const session = useCurrentUser()
  const room = useRoom(key)
  const swiping = room.data?.status === 'swiping'
  useRoomLive(key, room.isSuccess)
  const deck = useRoomDeck(key, swiping)
  const progress = useRoomProgress(key, swiping)
  const { vote } = useCastVote(key)
  const finish = useFinishRoom(key)

  if (room.isPending || session.isPending) return <LoadingState label="Opening the room…" />
  if (room.isError && room.error instanceof ApiError && (room.error.status === 401 || room.error.code === 'room.not_member')) {
    return <Navigate to={`/rooms/${encodeURIComponent(key)}`} replace />
  }
  if (room.isError) return <ErrorState error={room.error} onRetry={() => void room.refetch()} />

  const { status } = room.data
  const base = `/rooms/${encodeURIComponent(room.data.code)}`
  if (status === 'lobby') return <Navigate to={base} replace />
  if (status === 'result') return <Navigate to={`${base}/result`} replace />
  if (status === 'closed') {
    return (
      <div role="alert" className="mx-auto flex w-full max-w-md flex-col gap-3">
        <p className="font-bold">{toUserMessage(new ApiError({ status: 409, code: 'room.closed' }))}</p>
        <Link to="/" className="font-bold text-pink-text underline">
          Back to Tonight
        </Link>
      </div>
    )
  }
  if (deck.isPending) return <LoadingState label="Dealing the deck…" />
  if (deck.isError) return <ErrorState error={deck.error} onRetry={() => void deck.refetch()} />

  const items = deck.data
  const current = items.find((i) => !i.swipe)
  const next = items.filter((i) => !i.swipe)[1]
  const done = items.length - items.filter((i) => !i.swipe).length
  const total = progress.data?.deckSize ?? room.data.deckSize
  const stillSwiping = progress.data ? progress.data.members.filter((m) => m.votedCount < progress.data.deckSize).length : null

  return (
    <section className="mx-auto flex w-full max-w-sm flex-col gap-5 pb-4 lg:max-w-4xl">
      <div className="flex items-center justify-between">
        <MonoLabel>
          {stillSwiping !== null ? `LIVE · ${stillSwiping} SWIPING` : 'LIVE'} · {done} OF {total}
        </MonoLabel>
        {room.data.me.isHost && (
          <Button
            variant="outline"
            disabled={finish.isPending}
            onClick={() => finish.mutate(undefined, { onSuccess: () => void navigate(`${base}/result`), onError: notify.apiError })}
          >
            Finish now
          </Button>
        )}
      </div>

      {current ? (
        <>
          <div className="relative mx-auto w-full max-w-sm">
            {next && (
              <div aria-hidden="true" className="absolute inset-x-3 top-3 -z-10 h-full rounded-card border-[2.5px] border-ink bg-beige" />
            )}
            <SwipeCard key={current.id} film={current.film} onSwipe={(direction) => vote(current.id, direction)} />
          </div>
          <SwipeButtons onSwipe={(direction) => vote(current.id, direction)} />
        </>
      ) : (
        <div className="flex flex-col items-start gap-3">
          <h1 className="text-3xl font-extrabold tracking-tight">You&apos;re done</h1>
          <p className="text-ink-muted">Waiting for the others to finish. The result appears here as soon as the room decides.</p>
          <LoadingState label="Waiting for the room…" />
        </div>
      )}
    </section>
  )
}
