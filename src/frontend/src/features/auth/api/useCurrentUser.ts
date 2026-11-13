import { useQuery } from '@tanstack/react-query'
import { api, ApiError, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { CurrentUser } from '../types'

/**
 * The session: the signed-in user or guest, or `null` when there is none (a 401 is an answer, not a failure).
 * Every other error (network, 5xx) stays an error so a blip doesn't look like "signed out".
 */
export function useCurrentUser() {
  return useQuery({
    queryKey: queryKeys.auth.me,
    queryFn: async (): Promise<CurrentUser | null> => {
      try {
        const { data } = await api.GET('/api/auth/me')
        return requireData(data)
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
  })
}
