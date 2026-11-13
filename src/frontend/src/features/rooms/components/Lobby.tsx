import { useNavigate } from 'react-router'
import { toUserMessage } from '@/shared/api'
import { notify } from '@/shared/notify'
import { Button, MonoLabel, ShareIcon, TicketCard } from '@/shared/ui'
import { useLeaveRoom } from '../api/useLeaveRoom'
import { useStartRoom } from '../api/useStartRoom'
import { MODE_LABEL, readyCount } from '../roomCode'
import type { Room } from '../types'

/** Copies the invite link, or opens the phone's share sheet where there is one. Failure to share is not an error worth a toast. */
async function shareInvite(room: Room) {
  const url = `${window.location.origin}/rooms/${encodeURIComponent(room.code)}`
  try {
    if (typeof navigator.share === 'function') {
      await navigator.share({ title: room.name, text: `Join "${room.name}" on Ithaca`, url })
      return
    }
    await navigator.clipboard.writeText(url)
    notify.success('Link copied')
  } catch (error) {
    // Closing the share sheet rejects with AbortError: nothing to report.
    if (error instanceof DOMException && error.name === 'AbortError') return
    notify.error("Couldn't copy the link. Share the room code instead.")
  }
}

/** "Group — lobby": the ticket with the code, who is here and ready, and the host's start button. The server enforces host-only actions. */
export function Lobby({ room }: { room: Room }) {
  const navigate = useNavigate()
  const start = useStartRoom(room.code)
  const leave = useLeaveRoom(room.code)
  const host = room.members.find((m) => m.isHost)

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6">
      <TicketCard stubLabel="ROOM CODE" stubValue={room.code}>
        <div className="flex flex-col gap-2">
          <MonoLabel>{MODE_LABEL[room.mode]}</MonoLabel>
          <h1 className="text-3xl font-extrabold tracking-tight">{room.name}</h1>
          {room.deckSize > 0 && <p className="text-ink-muted">{room.deckSize} films to swipe</p>}
        </div>
      </TicketCard>

      <Button variant="yellow" onClick={() => void shareInvite(room)} className="w-full">
        <ShareIcon /> Invite friends
      </Button>

      <section aria-labelledby="members-heading" className="flex flex-col gap-3">
        <MonoLabel>
          <span id="members-heading">
            Here now · {room.members.length} of {room.maxMembers}
          </span>
        </MonoLabel>
        <ul className="flex flex-col gap-2">
          {room.members.map((member) => (
            <li
              key={member.id}
              className="flex min-h-12 items-center justify-between gap-3 rounded-control border-2 border-ink bg-cream px-4"
            >
              <span className="font-bold">
                {member.displayName}
                {member.id === room.me.id && <span className="font-normal text-ink-muted"> (you)</span>}
              </span>
              <span className="flex items-center gap-2">
                {member.isHost && <MonoLabel>Host</MonoLabel>}
                {member.kind === 'guest' && <MonoLabel>Guest</MonoLabel>}
                <span
                  className={`rounded-full border-2 border-ink px-2 py-0.5 font-mono text-xs uppercase ${member.isReady ? 'bg-yellow' : 'bg-beige'}`}
                >
                  {member.isReady ? 'Ready' : 'Waiting'}
                </span>
              </span>
            </li>
          ))}
        </ul>
      </section>

      {start.isError && (
        <p role="alert" className="font-bold text-pink-text">
          {toUserMessage(start.error)}
        </p>
      )}

      {room.me.isHost ? (
        <Button size="lg" disabled={start.isPending} onClick={() => start.mutate()} className="w-full">
          {start.isPending ? 'Starting…' : `Start swiping · ${readyCount(room.members)}`}
        </Button>
      ) : (
        <p className="text-center text-ink-muted">
          Waiting for {host?.displayName ?? 'the host'} to start · {readyCount(room.members)}
        </p>
      )}

      <Button
        variant="outline"
        disabled={leave.isPending}
        onClick={() => leave.mutate(undefined, { onSuccess: () => void navigate('/', { replace: true }), onError: notify.apiError })}
        className="w-full"
      >
        Leave room
      </Button>
    </div>
  )
}
