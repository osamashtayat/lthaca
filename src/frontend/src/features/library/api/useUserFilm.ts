import { useQuery } from '@tanstack/react-query'
import { api, ApiError, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { UserFilmState } from '../types'

/**
 * The signed-in user's state for one film (liked / saved / seen / rating; all false when untouched).
 * `enabled` is false for visitors: the call would be a 401. Only the flags are returned, the film comes from the films feature.
 */
export function useUserFilm(tmdbId: number, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.library.detail(tmdbId),
    queryFn: async (): Promise<UserFilmState> => {
      const { data } = await api.GET('/api/library/{tmdbId}', { params: { path: { tmdbId } } })
      const { liked, onWatchlist, seen, rating } = requireData(data)
      return { liked, onWatchlist, seen, rating }
    },
    enabled: enabled && Number.isInteger(tmdbId) && tmdbId > 0,
    // A 401 here means the session ended; the session query decides what the page shows.
    retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 1,
  })
}
