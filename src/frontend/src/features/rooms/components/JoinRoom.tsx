import { useState } from 'react'
import type { FormEvent } from 'react'
import { ApiError, serverFieldError, toUserMessage } from '@/shared/api'
import { Button, ErrorState, LoadingState, MonoLabel, TextField, TicketCard } from '@/shared/ui'
import { useJoinRoom } from '../api/useJoinRoom'
import { useRoomPreview } from '../api/useRoomPreview'
import { MODE_LABEL } from '../roomCode'

/**
 * What a non-member sees at `/rooms/:code`: the room preview and a way in. A signed-in user joins in one tap; a visitor
 * types a display name and joins as a guest (no account). Name rules are the server's: its field error shows here.
 */
export function JoinRoom({ code, isUser }: { code: string; isUser: boolean }) {
  const preview = useRoomPreview(code)
  const join = useJoinRoom(code)
  const [name, setName] = useState('')

  if (preview.isPending) return <LoadingState label="Finding the room…" />
  if (preview.isError) return <ErrorState error={preview.error} onRetry={() => void preview.refetch()} />
  const room = preview.data
  const closed = room.status === 'closed'
  const full = room.memberCount >= room.maxMembers

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    join.mutate(isUser ? null : name)
  }

  const fieldError = serverFieldError(join.error, 'displayName')

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-6">
      <TicketCard stubLabel={`${room.memberCount} OF ${room.maxMembers} HERE`} stubValue={room.code}>
        <div className="flex flex-col gap-2">
          <MonoLabel>{MODE_LABEL[room.mode]}</MonoLabel>
          <h1 className="text-3xl font-extrabold tracking-tight">{room.name}</h1>
          <p className="text-ink-muted">Hosted by {room.hostDisplayName}</p>
        </div>
      </TicketCard>

      {closed ? (
        <p role="alert" className="font-bold">
          {toUserMessage(new ApiError({ status: 409, code: 'room.closed' }))}
        </p>
      ) : (
        <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          {!isUser && (
            <TextField
              label="Your name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              autoComplete="nickname"
              maxLength={30}
              error={fieldError}
            />
          )}
          {join.isError && !fieldError && (
            <p role="alert" className="font-bold text-pink-text">
              {toUserMessage(join.error)}
            </p>
          )}
          <Button type="submit" size="lg" disabled={join.isPending || full} className="w-full">
            {join.isPending ? 'Joining…' : isUser ? 'Join the room' : 'Join as a guest'}
          </Button>
          {full && <p className="text-ink-muted">{toUserMessage(new ApiError({ status: 409, code: 'room.full' }))}</p>}
        </form>
      )}
    </div>
  )
}
