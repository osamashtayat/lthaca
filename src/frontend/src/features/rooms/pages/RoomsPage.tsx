import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { isUser, useCurrentUser } from '@/features/auth'
import { serverFieldError, toUserMessage } from '@/shared/api'
import { Button, LoadingState, SegmentedControl, TextField, TicketCard } from '@/shared/ui'
import { useCreateRoom } from '../api/useCreateRoom'
import { MODE_OPTIONS, normalizeRoomCode } from '../roomCode'
import type { RoomMode } from '../types'

/** "Movie nights": start a room (signed-in users) or enter a code to join one (anyone, guests included). */
export function RoomsPage() {
  const navigate = useNavigate()
  const session = useCurrentUser()
  const create = useCreateRoom()
  const [name, setName] = useState('')
  const [mode, setMode] = useState<RoomMode>('justSwipe')
  const [code, setCode] = useState('')

  if (session.isPending) return <LoadingState label="Loading…" />

  function onCreate(e: FormEvent) {
    e.preventDefault()
    create.mutate({ name, mode, startsAt: null }, { onSuccess: (room) => void navigate(`/rooms/${encodeURIComponent(room.code)}`) })
  }

  function onJoin(e: FormEvent) {
    e.preventDefault()
    const clean = normalizeRoomCode(code)
    if (clean) void navigate(`/rooms/${encodeURIComponent(clean)}`)
  }

  const nameError = serverFieldError(create.error, 'name')

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-4xl font-extrabold tracking-tight">Movie nights</h1>
        <p className="text-ink-muted">Pick a movie together. Friends join with a link, no account needed.</p>
      </div>

      {isUser(session.data) ? (
        <TicketCard stubLabel="ADMIT 2–8" stubValue="+">
          <form noValidate onSubmit={onCreate} className="flex flex-col gap-4">
            <TextField
              label="Room name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Friday Film Club"
              maxLength={40}
              error={nameError}
            />
            <SegmentedControl label="How to pick" options={MODE_OPTIONS} value={mode} onChange={setMode} />
            {create.isError && !nameError && (
              <p role="alert" className="font-bold text-pink-text">
                {toUserMessage(create.error)}
              </p>
            )}
            <Button type="submit" size="lg" disabled={create.isPending} className="w-full">
              {create.isPending ? 'Creating…' : 'Create room'}
            </Button>
          </form>
        </TicketCard>
      ) : (
        <p>
          <Link to="/sign-in?next=%2Frooms" className="font-bold text-pink-text underline">
            Sign in
          </Link>{' '}
          to host a room.
        </p>
      )}

      <form noValidate onSubmit={onJoin} className="flex items-end gap-2">
        <div className="flex-1">
          <TextField
            label="Have a room code?"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            autoComplete="off"
            spellCheck={false}
            placeholder="POP-428"
            className="w-full"
          />
        </div>
        <Button type="submit" variant="yellow" className="mb-px h-12">
          Join
        </Button>
      </form>
    </div>
  )
}
