import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { RoomDeckItem } from '../types'

/** The shared deck; each item's `swipe` is the caller's own vote. Refetched on `deckExtended` and after a reconnect. */
export function useRoomDeck(code: string, enabled = true) {
  const key = normalizeRoomCode(code)
  return useQuery({
    queryKey: queryKeys.rooms.deck(key),
    queryFn: async (): Promise<RoomDeckItem[]> => {
      const { data } = await api.GET('/api/rooms/{code}/deck', { params: { path: { code: key }, query: { fromPosition: 0 } } })
      return requireData(data)
    },
    enabled: enabled && key.length > 0,
  })
}
