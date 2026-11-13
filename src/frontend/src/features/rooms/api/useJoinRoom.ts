import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { Room } from '../types'

/**
 * Joins as the caller: a user under their account name, an anonymous visitor as a guest with `displayName` (the server
 * sets the guest cookie). The session changes for a guest, so the cached `auth.me` answer is refetched.
 */
export function useJoinRoom(code: string) {
  const queryClient = useQueryClient()
  const key = normalizeRoomCode(code)
  return useMutation({
    mutationFn: async (displayName: string | null): Promise<Room> => {
      const { data } = await api.POST('/api/rooms/{code}/join', { params: { path: { code: key } }, body: { displayName } })
      return requireData(data)
    },
    onSuccess: (room) => {
      queryClient.setQueryData(queryKeys.rooms.detail(key), room)
      void queryClient.invalidateQueries({ queryKey: queryKeys.auth.me })
    },
  })
}
