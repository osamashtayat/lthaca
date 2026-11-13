import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { Room } from '../types'

/** TODO(osamashtayat/lthaca#23): remove the polling once the hub has started / ready events.
 * How often the lobby re-reads the room: the hub has no "started" or "ready" event (see the contract issue), so it polls. */
const LOBBY_POLL_MS = 4_000

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
    refetchInterval: (query) => (query.state.data?.status === 'lobby' ? LOBBY_POLL_MS : false),
  })
}
