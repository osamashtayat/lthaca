import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef } from 'react'
import { api, ApiError } from '@/shared/api'
import { notify } from '@/shared/notify'
import { queryKeys } from '@/shared/query'
import { applySwipe, revertSwipe } from '../deckCache'
import type { Deck, SeenVerdict, SwipeDirection } from '../types'

type SwipeVars = { deckItemId: string; swipeId: string; direction: SwipeDirection; seenVerdict: SeenVerdict | null }

/**
 * One `POST …/swipes` per swipe. The card leaves the stack at once (cache edit); if the call fails it comes back
 * with a toast. Each card keeps one `swipeId` until its swipe is accepted, so trying the same card again can never
 * count twice (the server dedupes on it). Mutations never retry on their own.
 */
export function useSwipe(deckId: string) {
  const queryClient = useQueryClient()
  const key = queryKeys.decks.detail(deckId)
  const swipeIds = useRef(new Map<string, string>())

  const mutation = useMutation({
    mutationFn: async (vars: SwipeVars): Promise<void> => {
      await api.POST('/api/decks/{deckId}/swipes', { params: { path: { deckId } }, body: vars })
    },
    onMutate: async (vars) => {
      await queryClient.cancelQueries({ queryKey: key })
      queryClient.setQueryData<Deck>(key, (deck) =>
        deck ? applySwipe(deck, vars.deckItemId, vars.direction, vars.seenVerdict) : deck,
      )
    },
    onSuccess: (_data, vars) => swipeIds.current.delete(vars.deckItemId),
    onError: (error, vars) => {
      if (error instanceof ApiError && error.code === 'deck.already_swiped') {
        swipeIds.current.delete(vars.deckItemId) // it is recorded already: the card stays gone
        return
      }
      queryClient.setQueryData<Deck>(key, (deck) => (deck ? revertSwipe(deck, vars.deckItemId) : deck))
      notify.apiError(error)
      // The card isn't (or is no longer) in this deck: bringing it back would strand the user on it, so reload the deck.
      if (error instanceof ApiError && (error.code === 'deck.item_not_found' || error.code === 'deck.not_found')) {
        void queryClient.invalidateQueries({ queryKey: key })
      }
    },
  })

  function swipe(deckItemId: string, direction: SwipeDirection, seenVerdict: SeenVerdict | null = null) {
    const swipeId = swipeIds.current.get(deckItemId) ?? crypto.randomUUID()
    swipeIds.current.set(deckItemId, swipeId)
    mutation.mutate({ deckItemId, swipeId, direction, seenVerdict })
  }

  return { swipe }
}
