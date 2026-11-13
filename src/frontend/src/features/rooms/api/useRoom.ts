import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { Room } from '../types'

/** The room as a member sees it. 401 / 403 `room.not_member` mean "not in this room yet": show the preview and join form. */
export function useRoom(code: string) {
  const key = normalizeRoomCode(code)
  return useQuery({
    queryKey: queryKeys.rooms.detail(key),
    queryFn: async (): Promise<Room> => {
      const { data } = await api.GET('/api/rooms/{code}', { params: { path: { code: key } } })
      return requireData(data)
    },
    enabled: key.length > 0,
  })
}
