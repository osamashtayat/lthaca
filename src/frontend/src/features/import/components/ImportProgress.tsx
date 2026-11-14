import { Link } from 'react-router'
import { ButtonLink, MonoLabel, TicketCard } from '@/shared/ui'
import type { ImportJob } from '../types'
import { NotFoundList } from './NotFoundList'

const n = (value: number) => value.toLocaleString('en-US')

/** "312 of 1,204 films" while queued or running. */
export function ImportRunning({ job }: { job: ImportJob }) {
  const share = job.processedCount / Math.max(job.filmCount, 1)
  return (
    <TicketCard stubLabel="IMPORTING" stubValue={job.status === 'queued' ? '…' : `${Math.round(share * 100)}%`}>
      <div className="flex flex-col gap-3">
        <h2 className="text-2xl font-extrabold tracking-tight">{job.status === 'queued' ? 'Waiting to start…' : 'Importing your films…'}</h2>
        <progress className="sr-only" max={job.filmCount} value={job.processedCount} aria-label="Import progress" />
        <div aria-hidden="true" className="h-3 overflow-hidden rounded-full border-2 border-ink bg-cream">
          <div className="h-full bg-pink" style={{ width: `${share * 100}%` }} />
        </div>
        <p className="font-mono text-sm">
          {n(job.processedCount)} of {n(job.filmCount)} films
        </p>
        <p className="text-ink-muted">You can leave this page: the import keeps going.</p>
      </div>
    </TicketCard>
  )
}

/** The finished import: what changed in My Cinema, how many films weren't found, and the list. */
export function ImportResult({ job }: { job: ImportJob }) {
  const { changes } = job
  const lines = [
    `${n(changes.newFilms)} new in My Cinema`,
    changes.ratingsKept > 0
      ? `${n(changes.ratingsKept)} rating${changes.ratingsKept === 1 ? '' : 's'} you already had ${changes.ratingsKept === 1 ? 'was' : 'were'} kept`
      : null,
  ].filter(Boolean)

  return (
    <div className="flex flex-col gap-6">
      <TicketCard stubLabel="IMPORTED" stubValue={n(job.matchedCount)}>
        <div className="flex flex-col gap-3">
          <h2 className="text-2xl font-extrabold tracking-tight">Done</h2>
          <p className="font-bold">{lines.join(' · ')}</p>
          <p className="text-ink-muted">
            {n(changes.rated)} ratings, {n(changes.liked)} likes, {n(changes.watchlist)} on your watchlist, {n(changes.seen)} seen.
          </p>
          <MonoLabel>
            {n(job.notFoundCount)} {job.notFoundCount === 1 ? 'film' : 'films'} not found
          </MonoLabel>
          <ButtonLink to="/cinema" size="lg" className="w-full">
            Go to My Cinema
          </ButtonLink>
        </div>
      </TicketCard>
      {job.notFoundCount > 0 && <NotFoundList importId={job.id} listKept={job.notFoundListKept} />}
      <Link to="/" className="inline-flex min-h-11 items-center self-start font-bold text-pink-text">
        Back to Tonight
      </Link>
    </div>
  )
}
