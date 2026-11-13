import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import type { components } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { Room } from '../types'

type CreateRoomRequest = components['schemas']['CreateRoomRequest']

/** The host creates a room (signed-in users only; the server answers 401 otherwise). */
export function useCreateRoom() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: CreateRoomRequest): Promise<Room> => {
      const { data } = await api.POST('/api/rooms', { body })
      return requireData(data, 201)
    },
    onSuccess: (room) => queryClient.setQueryData(queryKeys.rooms.detail(room.code), room),
  })
}
