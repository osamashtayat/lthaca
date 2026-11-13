import type { QueryClient } from '@tanstack/react-query'
import { isNotSessionQuery, queryKeys } from '@/shared/query'
import type { CurrentUser } from '../types'

/** After sign-in/register: the new principal is known; everything else user-specific must be refetched. */
export function setSession(queryClient: QueryClient, user: CurrentUser) {
  queryClient.setQueryData(queryKeys.auth.me, user)
  void queryClient.invalidateQueries({ predicate: isNotSessionQuery })
}

/** After sign-out: drop every cached answer that belonged to the previous principal. */
export function clearSession(queryClient: QueryClient) {
  queryClient.removeQueries({ predicate: isNotSessionQuery })
  queryClient.setQueryData(queryKeys.auth.me, null)
}
