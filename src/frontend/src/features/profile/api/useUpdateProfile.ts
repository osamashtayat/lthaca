import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { Profile } from '../types'

/** Saves the display name. The header and menu read it from the session, so that is refetched too. */
export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (displayName: string): Promise<Profile> => {
      const { data } = await api.PUT('/api/profile', { body: { displayName } })
      return requireData(data)
    },
    onSuccess: (profile) => {
      queryClient.setQueryData(queryKeys.profile.me, profile)
      void queryClient.invalidateQueries({ queryKey: queryKeys.auth.me })
    },
  })
}
