import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { LibrarySummary } from '../types'

/** The three shelf counts (liked / watchlist / seen). */
export function useLibrarySummary() {
  return useQuery({
    queryKey: queryKeys.library.summary,
    queryFn: async (): Promise<LibrarySummary> => {
      const { data } = await api.GET('/api/library/summary')
      return requireData(data)
    },
  })
}
