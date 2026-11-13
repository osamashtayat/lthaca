import type { Film } from '@/features/films'
import { Button, Sheet } from '@/shared/ui'
import type { SeenVerdict } from '../types'

/** "Seen it": how was it? Loved it / Not for me, or Skip. Closing the sheet puts the card back. */
export function VerdictSheet({
  film,
  onChoose,
  onClose,
}: {
  film: Film | null
  onChoose: (verdict: SeenVerdict | null) => void
  onClose: () => void
}) {
  return (
    <Sheet open={film !== null} onClose={onClose} title="You've seen it">
      <p className="mb-4 text-ink-muted">{film ? `How was ${film.title}?` : ''}</p>
      <div className="flex flex-col gap-3">
        <Button variant="pink" size="lg" onClick={() => onChoose('loved')}>
          Loved it
        </Button>
        <Button variant="yellow" size="lg" onClick={() => onChoose('notForMe')}>
          Not for me
        </Button>
        <Button variant="outline" onClick={() => onChoose(null)}>
          Skip
        </Button>
      </div>
    </Sheet>
  )
}
