import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api'
import { clearSession } from './sessionCache'

export function useSignOut() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (): Promise<void> => {
      await api.POST('/api/auth/sign-out')
    },
    onSuccess: () => clearSession(queryClient),
  })
}
