import { Link } from 'react-router'
import { useLatestImport } from '@/features/import'
import { MonoLabel } from '@/shared/ui'
import { formatDate } from '../format'

const n = (value: number) => value.toLocaleString('en-US')

/** Entry point to `/import` (ADR-0024 Q3): an invitation, or the latest import's date and counts, read-only (§13). Silent while loading. */
export function LetterboxdRow() {
  const latest = useLatestImport()
  if (latest.isPending || latest.isError) return null
  const job = latest.data

  let summary = 'Bring your ratings, likes and watchlist into My Cinema.'
  let action = 'Import your history'
  if (job) {
    action = 'Import again'
    if (job.status === 'completed') {
      summary = `${formatDate(job.finishedAt ?? job.createdAt)} · ${n(job.matchedCount)} matched · ${n(job.notFoundCount)} not found`
    } else if (job.status === 'failed') {
      summary = `The import from ${formatDate(job.createdAt)} didn't finish.`
    } else {
      summary = `Importing ${n(job.processedCount)} of ${n(job.filmCount)} films…`
      action = 'See progress'
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <MonoLabel>Letterboxd</MonoLabel>
      <p className="text-ink-muted">{summary}</p>
      <Link to="/import" className="inline-flex min-h-11 items-center self-start font-bold text-pink-text">
        {action}
      </Link>
    </div>
  )
}
