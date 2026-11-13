import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'

/** The film's first YouTube trailer (`null` when there is none). Fetched only once `enabled` (the sheet opened). */
export function useTrailer(tmdbId: number, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.films.trailer(tmdbId),
    queryFn: async () => {
      const { data } = await api.GET('/api/films/{tmdbId}/trailer', { params: { path: { tmdbId } } })
      return requireData(data).trailer
    },
    enabled: enabled && Number.isInteger(tmdbId) && tmdbId > 0,
  })
}
