import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { Deck } from '../types'

export function useDeck(deckId: string) {
  return useQuery({
    queryKey: queryKeys.decks.detail(deckId),
    queryFn: async (): Promise<Deck> => {
      const { data } = await api.GET('/api/decks/{deckId}', { params: { path: { deckId } } })
      return requireData(data)
    },
    // Swipes are applied to the cache as they happen; a background refetch must not undo one in flight.
    refetchOnWindowFocus: false,
  })
}
