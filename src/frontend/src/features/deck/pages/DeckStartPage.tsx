import { useEffect, useRef } from 'react'
import { Navigate } from 'react-router'
import { ErrorState, LoadingState } from '@/shared/ui'
import { useCreateDeck } from '../api/useCreateDeck'

/** `/deck`: starts a "Just swipe" deck and moves on to `/deck/:deckId`. Visiting it is the whole interaction. */
export function DeckStartPage() {
  const create = useCreateDeck()
  const started = useRef(false)

  useEffect(() => {
    // Once per visit (StrictMode runs effects twice in dev); "Try again" below starts a fresh attempt.
    if (started.current) return
    started.current = true
    create.mutate()
  }, [create])

  if (create.isSuccess) return <Navigate to={`/deck/${create.data.id}`} replace />
  if (create.isError) return <ErrorState error={create.error} onRetry={() => create.mutate()} />
  return <LoadingState label="Shuffling the deck…" />
}
