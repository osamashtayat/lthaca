import { toUserMessage } from '@/shared/api/problem'
import { Button } from './Button'

/** A failed query, shown in place. Takes the thrown error and maps it to user text (never the server's detail). */
export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-start gap-3">
      <p>{toUserMessage(error)}</p>
      {onRetry && (
        <Button variant="outline" onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  )
}
