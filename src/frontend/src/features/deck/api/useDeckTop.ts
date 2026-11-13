import { useQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { DeckTop } from '../types'

/** The deck's top picks. A deck without right swipes answers 422 `deck.no_yeses`, which the page shows as a state. */
export function useDeckTop(deckId: string) {
  return useQuery({
    queryKey: queryKeys.decks.top(deckId),
    queryFn: async (): Promise<DeckTop> => {
      const { data } = await api.GET('/api/decks/{deckId}/top', { params: { path: { deckId } } })
      return requireData(data)
    },
  })
}
