import { Link } from 'react-router'
import { useTaste } from '@/features/taste'
import { Chip, MonoLabel } from '@/shared/ui'

/** The taste chips from `GET /api/taste` (§12), or how far the user is from having a taste. Silent while loading or on error. */
export function TasteSummary() {
  const taste = useTaste()
  if (!taste.data) return null
  const t = taste.data

  return (
    <div className="flex flex-col gap-3">
      <MonoLabel>Your taste</MonoLabel>
      {t.ready ? (
        <>
          <ul aria-label="Your top genres" className="flex flex-wrap gap-2">
            {t.topGenres.map((g) => (
              <li key={g.genre}>
                <Chip>
                  {g.genre} {g.percent}%
                </Chip>
              </li>
            ))}
          </ul>
          {t.notForYou.length > 0 && <p className="text-ink-muted">Not for you: {t.notForYou.join(', ')}</p>}
        </>
      ) : (
        <p className="text-ink-muted">
          {t.signalCount} of {t.signalsNeeded} films to read your taste.{' '}
          <Link to="/cinema" className="font-bold text-pink-text">
            Open My Cinema
          </Link>
        </p>
      )}
    </div>
  )
}
