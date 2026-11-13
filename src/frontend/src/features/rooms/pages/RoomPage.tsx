import { Link, Navigate, useParams } from 'react-router'
import { useCurrentUser, isUser } from '@/features/auth'
import { ApiError, toUserMessage } from '@/shared/api'
import { ErrorState, LoadingState } from '@/shared/ui'
import { useRoom } from '../api/useRoom'
import { useRoomLive } from '../api/useRoomLive'
import { JoinRoom } from '../components/JoinRoom'
import { Lobby } from '../components/Lobby'
import { normalizeRoomCode } from '../roomCode'

/** The caller is simply not in this room (yet): no session, or a session that belongs to someone else's room. */
const isNotMember = (error: unknown) => error instanceof ApiError && (error.status === 401 || error.code === 'room.not_member')

/**
 * `/rooms/:code` — the way in and the lobby. A member sees the lobby (live), anyone else the preview with a join form.
 * Once the host starts, the screen hands over to the swipe route (FE-11); a closed room ends here.
 */
export function RoomPage() {
  const { code = '' } = useParams()
  const key = normalizeRoomCode(code)
  const session = useCurrentUser()
  const room = useRoom(key)
  useRoomLive(key, room.isSuccess)

  if (room.isPending || session.isPending) return <LoadingState label="Opening the room…" />
  if (room.isError && isNotMember(room.error)) return <JoinRoom code={key} isUser={isUser(session.data)} />
  if (room.isError) return <ErrorState error={room.error} onRetry={() => void room.refetch()} />

  const { status } = room.data
  if (status === 'swiping') return <Navigate to={`/rooms/${encodeURIComponent(room.data.code)}/swipe`} replace />
  if (status === 'result') return <Navigate to={`/rooms/${encodeURIComponent(room.data.code)}/result`} replace />
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
  return <Lobby room={room.data} />
}
