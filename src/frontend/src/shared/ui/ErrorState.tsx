import { toUserMessage } from '@/shared/api/problem'

/** A failed query, shown in place. Takes the thrown error and maps it to user text (never the server's detail). */
export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-start gap-3">
      <p>{toUserMessage(error)}</p>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="min-h-11 rounded-control border-2 border-ink px-4 font-bold"
        >
          Try again
        </button>
      )}
    </div>
  )
}
