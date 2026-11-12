import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'

/** `GET /api/health`, for the dev status page only. */
export function useHealth() {
  return useQuery({
    queryKey: queryKeys.health,
    queryFn: async () => requireData((await api.GET('/api/health')).data),
  })
}
