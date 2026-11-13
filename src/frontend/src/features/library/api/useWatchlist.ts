import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api'
import { notify } from '@/shared/notify'
import { queryKeys } from '@/shared/query'

/**
 * Adds or removes one film from the watchlist without touching its other flags (`PUT`/`DELETE …/watchlist`, both
 * idempotent). For callers that don't hold the whole `UserFilm` state, like the deck card. The caller shows the
 * change at once and puts it back in its own `onError`; the toast is here.
 */
export function useWatchlist() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ tmdbId, on }: { tmdbId: number; on: boolean }): Promise<void> => {
      const params = { params: { path: { tmdbId } } }
      if (on) await api.PUT('/api/library/{tmdbId}/watchlist', params)
      else await api.DELETE('/api/library/{tmdbId}/watchlist', params)
    },
    onError: (error) => notify.apiError(error),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: queryKeys.library.all }),
  })
}
