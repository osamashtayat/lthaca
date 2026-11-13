import { Link, Navigate, useParams } from 'react-router'
import { ApiError, toUserMessage } from '@/shared/api'
import { ErrorState, LoadingState, MonoLabel, Poster, TicketCard, tmdbImageUrl } from '@/shared/ui'
import { useRoom } from '../api/useRoom'
import { useRoomLive } from '../api/useRoomLive'
import { useRoomResult } from '../api/useRoomResult'
import { normalizeRoomCode } from '../roomCode'
import type { RoomMember, RoomPick } from '../types'

const namesOf = (ids: readonly string[], members: readonly RoomMember[]) =>
  ids.map((id) => members.find((m) => m.id === id)?.displayName ?? 'Someone who left')

/** "Omar, Lina and Noor" */
const list = (names: string[]) => (names.length > 1 ? `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}` : (names[0] ?? ''))

function PickDetails({ pick, members }: { pick: RoomPick; members: readonly RoomMember[] }) {
  const yes = namesOf(pick.yes, members)
  const seen = namesOf(pick.seen, members)
  return (
    <ul className="flex flex-col gap-1 text-sm text-ink-muted">
      {yes.length > 0 && <li>{`${list(yes)} want${yes.length === 1 ? 's' : ''} to watch it`}</li>}
      {seen.map((name) => (
        <li key={name}>{`${name} passed: they've seen it`}</li>
      ))}
      {/* A7: only how many said no, never who. */}
      {pick.noCount > 0 && <li>{`${pick.noCount} said no`}</li>}
    </ul>
  )
}

/**
 * "It's a match" (everyone said yes: one film, a big moment) or "Close call" (the best compromise, up to three films
 * with who said yes or has seen it and only the number of noes). The server decides which; this renders it.
 */
export function RoomResultPage() {
  const { code = '' } = useParams()
  const key = normalizeRoomCode(code)
  const room = useRoom(key)
  useRoomLive(key, room.isSuccess)
  const result = useRoomResult(key, room.data?.status === 'result')

  if (room.isPending) return <LoadingState label="Opening the room…" />
  if (room.isError && room.error instanceof ApiError && (room.error.status === 401 || room.error.code === 'room.not_member')) {
    return <Navigate to={`/rooms/${encodeURIComponent(key)}`} replace /> // not in this room (yet): the join page
  }
  if (room.isError) return <ErrorState error={room.error} onRetry={() => void room.refetch()} />
  const base = `/rooms/${encodeURIComponent(room.data.code)}`
  if (room.data.status === 'lobby') return <Navigate to={base} replace />
  if (room.data.status === 'swiping') return <Navigate to={`${base}/swipe`} replace />
  if (room.data.status === 'closed') {
    return (
      <div role="alert" className="mx-auto flex w-full max-w-md flex-col gap-3">
        <p className="font-bold">{toUserMessage(new ApiError({ status: 409, code: 'room.closed' }))}</p>
        <Link to="/" className="font-bold text-pink-text underline">
          Back to Tonight
        </Link>
      </div>
    )
  }
  if (result.isPending) return <LoadingState label="Counting the votes…" />
  if (result.isError) return <ErrorState error={result.error} onRetry={() => void result.refetch()} />

  const { kind, picks } = result.data
  const members = room.data.members

  return (
    <section className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6 lg:max-w-3xl">
      <div className="flex flex-col gap-2">
        <MonoLabel>{room.data.name}</MonoLabel>
        {kind === 'match' ? (
          // The one "big moment": Shrikhand is allowed here.
          <h1 className="font-display text-5xl leading-tight">It&apos;s a match!</h1>
        ) : (
          <>
            <h1 className="text-4xl font-extrabold tracking-tight">Close call</h1>
            <p className="text-ink-muted">No film got everyone, so here are the best fits.</p>
          </>
        )}
      </div>

      {picks.length === 0 ? (
        <p className="text-ink-muted">Nobody said yes to anything this time.</p>
      ) : (
        <ol className="flex flex-col gap-6 lg:grid lg:grid-cols-3">
          {picks.map((pick) => (
            <li key={pick.film.tmdbId} className={kind === 'match' ? 'lg:col-span-3' : ''}>
              <TicketCard stubLabel={kind === 'match' ? 'ADMIT ALL' : `PICK ${pick.rank}`} stubValue={String(pick.rank)}>
                <div className="flex gap-4">
                  <Poster src={tmdbImageUrl(pick.film.posterPath, 'w342')} alt={`${pick.film.title} poster`} className="w-24 shrink-0" />
                  <div className="flex flex-col gap-2">
                    <h2 className="text-2xl leading-tight font-extrabold tracking-tight">
                      <Link to={`/films/${pick.film.tmdbId}`} className="no-underline">
                        {pick.film.title}
                      </Link>
                    </h2>
                    <MonoLabel>{[pick.film.releaseYear, pick.film.runtimeMinutes ? `${pick.film.runtimeMinutes} min` : null].filter(Boolean).join(' · ')}</MonoLabel>
                    <PickDetails pick={pick} members={members} />
                  </div>
                </div>
              </TicketCard>
            </li>
          ))}
        </ol>
      )}

      <Link to="/" className="inline-flex min-h-11 items-center font-bold text-pink-text">
        Back to Tonight
      </Link>
    </section>
  )
}
