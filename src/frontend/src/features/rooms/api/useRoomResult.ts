import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { RoomResult } from '../types'

/** The match or the best compromise. 409 `room.no_result` while the room is still swiping. */
export function useRoomResult(code: string, enabled = true) {
  const key = normalizeRoomCode(code)
  return useQuery({
    queryKey: queryKeys.rooms.result(key),
    queryFn: async (): Promise<RoomResult> => {
      const { data } = await api.GET('/api/rooms/{code}/result', { params: { path: { code: key } } })
      return requireData(data)
    },
    enabled: enabled && key.length > 0,
  })
}
