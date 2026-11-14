import { Link } from 'react-router'
import { LetterboxdHelpSheet } from '@/features/import'
import { ButtonLink, TicketCard } from '@/shared/ui'
import type { Taste } from '../types'

/**
 * The empty state (ADR-0024): By taste needs a few films the user loves, so instead of a dead end this offers the
 * Letterboxd import (with its "?" steps) or "Just swipe instead". Shown only when the server says `ready: false`.
 */
export function ImportYourLetterboxd({ taste }: { taste: Taste }) {
  return (
    <section className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-4xl font-extrabold tracking-tight">By taste</h1>
        <p className="text-ink-muted">We need a few films you love first.</p>
      </div>

      <TicketCard stubLabel="LOVED SO FAR" stubValue={`${taste.signalCount}/${taste.signalsNeeded}`}>
        <div className="flex flex-col gap-4">
          <h2 className="text-2xl font-extrabold tracking-tight">Import your Letterboxd</h2>
          <p className="text-ink-muted">
            Bring your ratings and likes over and your deck is built from them. You can also like or rate films here to
            get to {taste.signalsNeeded}.
          </p>
          <div className="flex items-center gap-3">
            <ButtonLink to="/import" size="lg" className="flex-1">
              Import from Letterboxd
            </ButtonLink>
            <LetterboxdHelpSheet />
          </div>
        </div>
      </TicketCard>

      <ButtonLink to="/deck" variant="outline" className="w-full">
        Just swipe instead
      </ButtonLink>
      <Link to="/browse" className="inline-flex min-h-11 items-center self-start font-bold text-pink-text">
        Or browse films to like
      </Link>
    </section>
  )
}
