import { useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/shared/query'
import { useRoomHub, type RoomEventHandlers } from '@/shared/realtime'
import { normalizeRoomCode } from '../roomCode'

/**
 * Keeps a room screen live (docs/realtime.md → Client rules): joins the room's hub group and turns each event into a
 * refetch of the room. Payloads are never trusted for data; the API is. A reconnect refetches too (events may be missed).
 */
export function useRoomLive(code: string, enabled: boolean) {
  const queryClient = useQueryClient()
  const key = normalizeRoomCode(code)
  const refetchRoom = () => void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.detail(key) })
  const handlers: RoomEventHandlers = {
    memberJoined: refetchRoom,
    memberLeft: refetchRoom,
    roomMatched: refetchRoom,
    roomClosed: refetchRoom,
  }
  return useRoomHub({ enabled, room: key, handlers, onSynced: refetchRoom })
}
