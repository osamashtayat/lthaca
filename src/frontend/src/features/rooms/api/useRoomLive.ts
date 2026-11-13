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
  // `exact`: the room itself only. The deck holds the member's own optimistic votes and is refetched by its own events.
  const refetchRoom = () => void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.detail(key), exact: true })
  const refetchProgress = () => void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.progress(key) })
  const refetchDeck = () => void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.deck(key) })
  // After a (re)connect events may have been missed: everything for this room (no `exact`).
  const refetchAll = () => void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.detail(key) })
  const handlers: RoomEventHandlers = {
    memberJoined: refetchRoom,
    memberLeft: refetchRoom,
    memberReady: refetchRoom,
    roomStarted: refetchRoom,
    voteCast: refetchProgress,
    deckExtended: refetchDeck,
    roomMatched: refetchRoom,
    roomClosed: refetchRoom,
  }
  return useRoomHub({ enabled, room: key, handlers, onSynced: refetchAll })
}
