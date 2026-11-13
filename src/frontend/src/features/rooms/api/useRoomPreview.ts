import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { RoomPreview } from '../types'

/** What a non-member sees before joining. Public. */
export function useRoomPreview(code: string, enabled = true) {
  const key = normalizeRoomCode(code)
  return useQuery({
    queryKey: queryKeys.rooms.preview(key),
    queryFn: async (): Promise<RoomPreview> => {
      const { data } = await api.GET('/api/rooms/{code}/preview', { params: { path: { code: key } } })
      return requireData(data)
    },
    enabled: enabled && key.length > 0,
  })
}
