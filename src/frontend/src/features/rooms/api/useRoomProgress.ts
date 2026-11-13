import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { RoomProgress } from '../types'

/** How far each member is ("4 OF 12", "LIVE · 3 SWIPING"). Counts only: never anyone's votes. Refetched on `voteCast`. */
export function useRoomProgress(code: string, enabled = true) {
  const key = normalizeRoomCode(code)
  return useQuery({
    queryKey: queryKeys.rooms.progress(key),
    queryFn: async (): Promise<RoomProgress> => {
      const { data } = await api.GET('/api/rooms/{code}/progress', { params: { path: { code: key } } })
      return requireData(data)
    },
    enabled: enabled && key.length > 0,
  })
}
