import { useEffect, useRef, useState } from 'react'
import { ROOM_EVENTS, type RoomEvent } from './events'
import { createRoomHubConnection, statusOf, type RoomHubStatus } from './roomHub'

export type RoomEventHandlers = Partial<Record<RoomEvent, (payload: unknown) => void>>

/**
 * The ONE way to talk to RoomHub: opens a connection while mounted and reports its status.
 * `handlers` maps contract event names to callbacks (typically query invalidations from a feature api/ hook);
 * the latest handlers are always used, without reconnecting.
 * SKELETON: connection only. Joining a room (a hub method) is added by the rooms feature.
 */
export function useRoomHub({ enabled = true, handlers = {} }: { enabled?: boolean; handlers?: RoomEventHandlers } = {}) {
  const [status, setStatus] = useState<RoomHubStatus>(enabled ? 'connecting' : 'disconnected')
  const handlersRef = useRef(handlers)

  useEffect(() => {
    handlersRef.current = handlers
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
    connection.onreconnecting(sync)
    connection.onreconnected(sync)
    connection.onclose(sync)
    connection.start().then(sync, sync)
    return () => {
      stopped = true
      void connection.stop()
    }
  }, [enabled])

  return { status }
}
