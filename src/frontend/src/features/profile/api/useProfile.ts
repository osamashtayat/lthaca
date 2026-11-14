import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { Profile } from '../types'

/** The signed-in user's name, email and join date (api-contract §14). User-only: a guest gets 401. */
export function useProfile() {
  return useQuery({
    queryKey: queryKeys.profile.me,
    queryFn: async (): Promise<Profile> => {
      const { data } = await api.GET('/api/profile')
      return requireData(data)
    },
  })
}
