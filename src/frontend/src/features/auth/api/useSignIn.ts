import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import type { components } from '@/shared/api'
import type { CurrentUser } from '../types'
import { setSession } from './sessionCache'

type SignInRequest = components['schemas']['SignInRequest']

export function useSignIn() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: SignInRequest): Promise<CurrentUser> => {
      const { data } = await api.POST('/api/auth/sign-in', { body })
      return requireData(data)
    },
    onSuccess: (user) => setSession(queryClient, user),
  })
}
