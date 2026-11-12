import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { Film } from '../types'

/**
 * REFERENCE SLICE (copy this shape): one hook per endpoint, key from the central factory, the typed `api` client,
 * `requireData` so an empty 200 is an error. Errors arrive as ApiError; the page decides how to show them.
 */
export function useFilm(tmdbId: number) {
  return useQuery({
    queryKey: queryKeys.films.detail(tmdbId),
    queryFn: async (): Promise<Film> => {
      const { data } = await api.GET('/api/films/{tmdbId}', { params: { path: { tmdbId } } })
      return requireData(data)
    },
    enabled: Number.isInteger(tmdbId) && tmdbId > 0,
  })
}
