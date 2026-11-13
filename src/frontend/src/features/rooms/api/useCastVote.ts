import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef } from 'react'
import { api, ApiError } from '@/shared/api'
import { notify } from '@/shared/notify'
import { queryKeys } from '@/shared/query'
import { normalizeRoomCode } from '../roomCode'
import type { RoomDeckItem, VoteDirection } from '../types'

type VoteVars = { deckItemId: string; voteId: string; direction: VoteDirection }

const withVote = (items: RoomDeckItem[] | undefined, id: string, direction: VoteDirection | null) =>
  items?.map((i) => (i.id === id ? { ...i, swipe: direction ? { direction, seenVerdict: null } : null } : i))

/**
 * One `POST …/votes` per vote (ADR-0015). The card leaves the stack at once; a failure puts it back with a toast.
 * A card keeps one `voteId` until its vote is accepted, so asking twice can never count twice. Never retried automatically.
 */
export function useCastVote(code: string) {
  const queryClient = useQueryClient()
  const key = normalizeRoomCode(code)
  const deckKey = queryKeys.rooms.deck(key)
  const voteIds = useRef(new Map<string, string>())

  const mutation = useMutation({
    mutationFn: async (vars: VoteVars): Promise<void> => {
      await api.POST('/api/rooms/{code}/votes', { params: { path: { code: key } }, body: { ...vars, seenVerdict: null } })
    },
    onMutate: async (vars) => {
      await queryClient.cancelQueries({ queryKey: deckKey })
      queryClient.setQueryData<RoomDeckItem[]>(deckKey, (items) => withVote(items, vars.deckItemId, vars.direction))
    },
    onSuccess: (_data, vars) => {
      voteIds.current.delete(vars.deckItemId)
      void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.progress(key) })
    },
    onError: (error, vars) => {
      if (error instanceof ApiError && error.code === 'room.already_voted') {
        voteIds.current.delete(vars.deckItemId) // recorded already: the card stays gone
        return
      }
      queryClient.setQueryData<RoomDeckItem[]>(deckKey, (items) => withVote(items, vars.deckItemId, null))
      notify.apiError(error)
      // The room moved on (matched, closed) or the item is gone: re-read instead of leaving the user on a dead card.
      if (error instanceof ApiError && ['room.invalid_state', 'room.closed', 'deck.item_not_found'].includes(error.code)) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.rooms.detail(key) })
      }
    },
  })

  function vote(deckItemId: string, direction: VoteDirection) {
    const voteId = voteIds.current.get(deckItemId) ?? crypto.randomUUID()
    voteIds.current.set(deckItemId, voteId)
    mutation.mutate({ deckItemId, voteId, direction })
  }

  return { vote }
}
