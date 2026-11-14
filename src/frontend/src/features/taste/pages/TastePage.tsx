import { ErrorState, LoadingState } from '@/shared/ui'
import { useTaste } from '../api/useTaste'
import { ImportYourLetterboxd } from '../components/ImportYourLetterboxd'
import { TasteBoard } from '../components/TasteBoard'

/** `/taste` (user-only): the board when the server says `ready`, otherwise the "Import your Letterboxd" screen. */
export function TastePage() {
  const taste = useTaste()
  if (taste.isPending) return <LoadingState label="Reading your taste…" />
  if (taste.isError) return <ErrorState error={taste.error} onRetry={() => void taste.refetch()} />
  return taste.data.ready ? (
    <TasteBoard taste={taste.data} onNotReady={() => void taste.refetch()} />
  ) : (
    <ImportYourLetterboxd taste={taste.data} />
  )
}
