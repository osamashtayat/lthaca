import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { Room, RoomResult } from '../types'

/** Host only: ends swiping now. Answers with the result (a compromise if there is no match yet). */
export function useFinishRoom(code: string) {
  const queryClient = useQueryClient()
  const key = normalizeRoomCode(code)
  return useMutation({
    mutationFn: async (): Promise<RoomResult> => {
      const { data } = await api.POST('/api/rooms/{code}/finish', { params: { path: { code: key } } })
      return requireData(data)
    },
    onSuccess: (result) => {
      queryClient.setQueryData(queryKeys.rooms.result(key), result)
      // Mark the cached room decided right away, so the result screen doesn't bounce back to the deck on a stale `swiping`.
      queryClient.setQueryData<Room>(queryKeys.rooms.detail(key), (room) => (room ? { ...room, status: 'result' } : room))
      void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.detail(key), exact: true })
    },
  })
}
