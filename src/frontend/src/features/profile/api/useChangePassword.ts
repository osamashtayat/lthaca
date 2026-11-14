import { useMutation } from '@tanstack/react-query'
import { api } from '@/shared/api'

type ChangePasswordInput = { currentPassword: string; newPassword: string }

/** The session stays signed in; the new `XSRF-TOKEN` the server sends is picked up by the client on the next call. */
export function useChangePassword() {
  return useMutation({
    mutationFn: async (body: ChangePasswordInput): Promise<void> => {
      await api.PUT('/api/profile/password', { body })
    },
  })
}
