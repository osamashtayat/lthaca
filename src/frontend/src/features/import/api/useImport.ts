import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { ImportJob } from '../types'

/** While the job is queued or running the client polls; there is no real-time event for imports (api-contract §13). */
const POLL_MS = 2_000

const isActive = (job: ImportJob | undefined) => job?.status === 'queued' || job?.status === 'running'

/**
 * One import, polled every ~2 s only while `queued` or `running`. When it completes, everything the import changed is
 * re-read once: My Cinema and its counts (§7) and the taste readiness that likes and ratings feed (§12).
 */
export function useImport(importId: string | null, initial?: ImportJob) {
  const queryClient = useQueryClient()
  const refreshed = useRef<string | null>(null)
  const query = useQuery({
    queryKey: queryKeys.imports.detail(importId ?? ''),
    queryFn: async (): Promise<ImportJob> => {
      const { data } = await api.GET('/api/imports/{importId}', { params: { path: { importId: importId ?? '' } } })
      return requireData(data)
    },
    enabled: importId !== null,
    ...(initial ? { initialData: initial } : {}),
    refetchInterval: (q) => (isActive(q.state.data) ? POLL_MS : false),
  })

  const status = query.data?.status
  useEffect(() => {
    if (status === 'completed' && importId && refreshed.current !== importId) {
      refreshed.current = importId
      void queryClient.invalidateQueries({ queryKey: queryKeys.library.all })
      void queryClient.invalidateQueries({ queryKey: queryKeys.taste.all })
    }
  }, [status, importId, queryClient])

  return query
}
