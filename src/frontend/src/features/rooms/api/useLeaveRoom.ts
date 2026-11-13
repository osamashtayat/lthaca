import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'

/** Leaves the room: the cached room and preview are dropped, and the session is refetched (a guest's room ends with it). */
export function useLeaveRoom(code: string) {
  const queryClient = useQueryClient()
  const key = normalizeRoomCode(code)
  return useMutation({
    mutationFn: async (): Promise<void> => {
      await api.POST('/api/rooms/{code}/leave', { params: { path: { code: key } } })
    },
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: queryKeys.rooms.detail(key) })
      void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.preview(key) })
      void queryClient.invalidateQueries({ queryKey: queryKeys.auth.me })
    },
  })
}
