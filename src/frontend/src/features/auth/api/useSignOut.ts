import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api'
import { clearSession } from './sessionCache'

export function useSignOut() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (): Promise<void> => {
      // No result to read: a non-2xx throws ApiError from the client middleware, so failures still reach onError.
      await api.POST('/api/auth/sign-out')
    },
    onSuccess: () => clearSession(queryClient),
  })
}
