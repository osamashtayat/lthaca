import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { appendItems } from '../deckCache'
import type { Deck, DeckItem } from '../types'

/** Asks for the next batch (a POST: the server records it). `[]` means the deck has nothing left. */
export function useMoreItems(deckId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (): Promise<DeckItem[]> => {
      const { data } = await api.POST('/api/decks/{deckId}/items', { params: { path: { deckId } } })
      return requireData(data)
    },
    onSuccess: (items) =>
      queryClient.setQueryData<Deck>(queryKeys.decks.detail(deckId), (deck) => (deck ? appendItems(deck, items) : deck)),
  })
}
