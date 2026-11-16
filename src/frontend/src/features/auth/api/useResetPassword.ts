import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import type { components } from '@/shared/api'
import type { CurrentUser } from '../types'
import { setSession } from './sessionCache'

type ResetPasswordRequest = components['schemas']['ResetPasswordRequest']

/** On success the caller is signed in as the user (api-contract §4), exactly as after sign-in. */
export function useResetPassword() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: ResetPasswordRequest): Promise<CurrentUser> => {
      const { data } = await api.POST('/api/auth/reset-password', { body })
      return requireData(data)
    },
    onSuccess: (user) => setSession(queryClient, user),
  })
}
