import { useMutation } from '@tanstack/react-query'
import { api } from '@/shared/api'
import type { components } from '@/shared/api'

type ForgotPasswordRequest = components['schemas']['ForgotPasswordRequest']

/** Always 204 for a well-formed email (api-contract §4): the answer never says whether an account exists. */
export function useForgotPassword() {
  return useMutation({
    mutationFn: async (body: ForgotPasswordRequest): Promise<void> => {
      // No result to read: a non-2xx throws ApiError from the client middleware, so failures still reach onError.
      await api.POST('/api/auth/forgot-password', { body })
    },
  })
}
