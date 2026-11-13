import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import type { components } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'

type SetRoomMoodRequest = components['schemas']['SetRoomMoodRequest']
type RoomMoodDto = components['schemas']['RoomMoodDto']

/** The member's mood answer for tonight. Success means "ready": the room is refetched so the lobby shows it. */
export function useSetRoomMood(code: string) {
  const queryClient = useQueryClient()
  const key = normalizeRoomCode(code)
  return useMutation({
    mutationFn: async (body: SetRoomMoodRequest): Promise<RoomMoodDto> => {
      const { data } = await api.PUT('/api/rooms/{code}/mood', { params: { path: { code: key } }, body })
      return requireData(data)
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.detail(key) }),
  })
}
