import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData, type components } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { Deck } from '../types'

type CreateDeckRequest = components['schemas']['CreateDeckRequest']

/**
 * Starts a deck ("Just swipe" when called without a request, or the By-mood request); the response is the first
 * batch, so it goes straight into the cache.
 */
export function useCreateDeck() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (request: CreateDeckRequest | undefined = { source: 'justSwipe', mood: null }): Promise<Deck> => {
      const { data } = await api.POST('/api/decks', { body: request })
      return requireData(data)
    },
    onSuccess: (deck) => queryClient.setQueryData(queryKeys.decks.detail(deck.id), deck),
  })
}
