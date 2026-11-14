import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { notify } from '@/shared/notify'
import { queryKeys } from '@/shared/query'
import { toUserFilmState, type UserFilmState } from '../types'

/**
 * `PUT /api/library/{tmdbId}` replaces the whole state (api-contract §7, last write wins). The cache is updated
 * optimistically and put back if the call fails, with a toast. Mutations never retry (AGENTS conventions).
 */
export function useUpdateUserFilm(tmdbId: number) {
  const queryClient = useQueryClient()
  const key = queryKeys.library.detail(tmdbId)

  return useMutation({
    mutationKey: ['library', 'update', tmdbId],
    mutationFn: async (state: UserFilmState): Promise<UserFilmState> => {
      const { data } = await api.PUT('/api/library/{tmdbId}', { params: { path: { tmdbId } }, body: state })
      return toUserFilmState(requireData(data))
    },
    onMutate: async (state) => {
      await queryClient.cancelQueries({ queryKey: key })
      const previous = queryClient.getQueryData<UserFilmState>(key)
      queryClient.setQueryData<UserFilmState>(key, state)
      return { previous }
    },
    onError: (error, _state, context) => {
      queryClient.setQueryData(key, context?.previous)
      notify.apiError(error)
    },
    onSuccess: (saved) => queryClient.setQueryData(key, saved),
    onSettled: () => {
      // Refetch other library queries (shelves, counts) once the last pending write for this film has landed.
      if (queryClient.isMutating({ mutationKey: ['library', 'update', tmdbId] }) === 1) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.library.all })
        // Likes and ratings are what "By taste" is built from (api-contract §12): its readiness must be re-read.
        void queryClient.invalidateQueries({ queryKey: queryKeys.taste.all })
      }
    },
  })
}
