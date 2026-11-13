import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import type { components } from '@/shared/api'
import type { CurrentUser } from '../types'
import { setSession } from './sessionCache'

type RegisterRequest = components['schemas']['RegisterRequest']

export function useRegister() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: RegisterRequest): Promise<CurrentUser> => {
      const { data } = await api.POST('/api/auth/register', { body })
      return requireData(data, 201)
    },
    onSuccess: (user) => setSession(queryClient, user),
  })
}
