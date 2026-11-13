import { useEffect, useRef, useState } from 'react'
import { REALTIME_ENABLED } from '@/shared/api/mode'
import { ROOM_EVENTS, type RoomEvent } from './events'
import { createRoomHubConnection, HUB_METHODS, statusOf, type RoomHubStatus } from './roomHub'

export type RoomEventHandlers = Partial<Record<RoomEvent, (payload: unknown) => void>>

/**
 * The ONE way to talk to RoomHub: opens a connection while mounted and reports its status.
 * `handlers` maps contract event names to callbacks (typically query invalidations from a feature api/ hook);
 * the latest handlers are always used, without reconnecting. Never connects when `REALTIME_ENABLED` is false.
 * With `room` (a room code) the hook also invokes `JoinRoom(code)` once connected and again after every reconnect (the
 * server drops group membership with the connection), then calls `onSynced` so the screen can refetch what it missed.
 */
export function useRoomHub({
  enabled: wanted = true,
  handlers = {},
  room,
  onSynced,
}: { enabled?: boolean; handlers?: RoomEventHandlers; room?: string; onSynced?: () => void } = {}) {
  const enabled = wanted && REALTIME_ENABLED
  const [status, setStatus] = useState<RoomHubStatus>(enabled ? 'connecting' : 'disconnected')
  const handlersRef = useRef(handlers)
  const onSyncedRef = useRef(onSynced)

  useEffect(() => {
    handlersRef.current = handlers
    onSyncedRef.current = onSynced
  })

  useEffect(() => {
    if (!enabled) return
    const connection = createRoomHubConnection()
    let stopped = false
    for (const event of ROOM_EVENTS) {
      connection.on(event, (payload: unknown) => handlersRef.current[event]?.(payload))
    }
    const sync = () => {
      if (!stopped) setStatus(statusOf(connection))
    }
    // Joins the room's group; a failure (not a member, hub down) leaves the screen working on plain refetches.
    const joinRoom = async () => {
      sync()
      if (stopped || !room) return
      try {
        await connection.invoke(HUB_METHODS.joinRoom, room)
        if (!stopped) onSyncedRef.current?.()
      } catch {
        // Not fatal: the room screen still refetches on navigation and on its own polling.
      }
    }
    connection.onreconnecting(sync)
    connection.onreconnected(() => void joinRoom())
    connection.onclose(sync)
    connection.start().then(() => void joinRoom(), sync)
    return () => {
      stopped = true
      void connection.stop()
    }
  }, [enabled, room])

  return { status }
}
