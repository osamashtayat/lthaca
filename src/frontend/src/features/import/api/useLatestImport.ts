import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { ImportJob } from '../types'

/** The user's newest import, or `null` if they never imported: how `/import` resumes after a reload. */
export function useLatestImport() {
  return useQuery({
    queryKey: queryKeys.imports.latest,
    queryFn: async (): Promise<ImportJob | null> => {
      const { data } = await api.GET('/api/imports', { params: { query: { page: 1, pageSize: 1 } } })
      return requireData(data).items[0] ?? null
    },
  })
}
