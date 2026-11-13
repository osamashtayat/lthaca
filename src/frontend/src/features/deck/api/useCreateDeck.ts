import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { Deck } from '../types'

/** Starts a "Just swipe" deck; the response is the first batch, so it goes straight into the cache. */
export function useCreateDeck() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (): Promise<Deck> => {
      const { data } = await api.POST('/api/decks', { body: { source: 'justSwipe', mood: null } })
      return requireData(data)
    },
    onSuccess: (deck) => queryClient.setQueryData(queryKeys.decks.detail(deck.id), deck),
  })
}
