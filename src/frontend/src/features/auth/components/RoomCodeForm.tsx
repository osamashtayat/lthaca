import { useState } from 'react'
import { useNavigate } from 'react-router'
import { Button, MonoLabel, TextField } from '@/shared/ui'

/** "Have a room code?": sends the visitor to the room, whose own screen handles joining (guest or user). */
export function RoomCodeForm() {
  const navigate = useNavigate()
  const [code, setCode] = useState('')

  return (
    <form
      className="flex flex-col gap-3"
      onSubmit={(e) => {
        e.preventDefault()
        const clean = code.trim().toUpperCase()
        if (clean) void navigate(`/rooms/${encodeURIComponent(clean)}`)
      }}
    >
      <MonoLabel>Have a room code?</MonoLabel>
      <div className="flex items-end gap-2">
        <div className="flex-1">
          <TextField
            label="Room code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            autoCapitalize="characters"
            autoComplete="off"
            spellCheck={false}
            className="w-full uppercase"
          />
        </div>
        <Button type="submit" variant="yellow" className="mb-px h-12">
          Join
        </Button>
      </div>
    </form>
  )
}
