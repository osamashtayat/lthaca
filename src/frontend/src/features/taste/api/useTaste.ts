import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { Taste } from '../types'

/** The caller's taste: whether By taste is available yet, and the "taste receipt". User-only (401 otherwise). */
export function useTaste() {
  return useQuery({
    queryKey: queryKeys.taste.me,
    queryFn: async (): Promise<Taste> => {
      const { data } = await api.GET('/api/taste')
      return requireData(data)
    },
  })
}
