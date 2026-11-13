import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { Room } from '../types'

/** Host only (the server answers 403 `room.not_host` to anyone else). The room moves to `swiping`. */
export function useStartRoom(code: string) {
  const queryClient = useQueryClient()
  const key = normalizeRoomCode(code)
  return useMutation({
    mutationFn: async (): Promise<Room> => {
      const { data } = await api.POST('/api/rooms/{code}/start', { params: { path: { code: key } } })
      return requireData(data)
    },
    onSuccess: (room) => queryClient.setQueryData(queryKeys.rooms.detail(key), room),
    onError: () => void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.detail(key) }),
  })
}
