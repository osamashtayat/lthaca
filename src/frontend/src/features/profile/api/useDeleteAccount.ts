import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api'
import { clearSession } from '@/features/auth'

/** Deletes the account (needs the current password). The server signs the caller out; drop everything cached for them. */
export function useDeleteAccount() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (password: string): Promise<void> => {
      await api.DELETE('/api/profile', { body: { password } })
    },
    onSuccess: () => clearSession(queryClient),
  })
}
