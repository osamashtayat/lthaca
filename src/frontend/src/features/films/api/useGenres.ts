import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'

/** The genres present in our catalog, alphabetical: what the genre filters offer. */
export function useGenres() {
  return useQuery({
    queryKey: queryKeys.films.genres,
    queryFn: async (): Promise<string[]> => {
      const { data } = await api.GET('/api/films/genres')
      return requireData(data)
    },
    staleTime: 10 * 60_000, // changes only with the nightly sync
  })
}
