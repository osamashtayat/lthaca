import { useState } from 'react'
import { Link } from 'react-router'
import { isUser, RoomCodeForm, useCurrentUser } from '@/features/auth'
import { LoadingState, MonoLabel, SegmentedControl, TicketCard } from '@/shared/ui'
import { dayLabelFor, greetingFor } from '../greeting'

type Who = 'solo' | 'group'
const WHO = [
  { value: 'solo', label: 'Just me' },
  { value: 'group', label: 'With friends' },
] as const

const ticketLink =
  'inline-flex min-h-11 items-center justify-center rounded-full border-2 border-ink bg-ink px-5 font-extrabold text-cream no-underline'

/**
 * "Tonight": the home screen. A visitor can toggle and join by code; every other action leads to `/sign-in` (with
 * `next` set so they land where they were going). Who may do what is the server's call; this only picks the link.
 */
export function TonightPage({ now: given }: { now?: Date }) {
  const [now] = useState(() => given ?? new Date())
  const session = useCurrentUser()
  const [who, setWho] = useState<Who>('solo')

  if (session.isPending) return <LoadingState label="Loading…" />
  const user = isUser(session.data) ? session.data : null
  const signedIn = user !== null
  // FE-12 acceptance: visitors may toggle and join by code; every other action (hosting a room included) leads to sign-in.
  const to = (path: string) => (signedIn ? path : `/sign-in?next=${encodeURIComponent(path)}`)
  const name = user?.displayName ?? null

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6 lg:max-w-3xl">
      <div className="flex flex-col gap-2">
        <MonoLabel>{dayLabelFor(now)}</MonoLabel>
        <h1 className="text-4xl font-extrabold tracking-tight">
          {greetingFor(now)}
          {name ? `, ${name}` : ''}
        </h1>
        <p className="text-ink-muted">What are we watching tonight?</p>
      </div>

      <SegmentedControl label="Who is watching" options={WHO} value={who} onChange={setWho} />

      <div className="flex flex-col gap-6 lg:grid lg:grid-cols-2">
        <TicketCard
          className="lg:col-span-2"
          stubLabel={who === 'solo' ? 'ADMIT 1' : 'ADMIT 2–8'}
          stubValue={who === 'solo' ? '1' : '+'}
        >
          <div className="flex flex-col gap-3">
            <h2 className="text-2xl font-extrabold tracking-tight">{who === 'solo' ? 'Just swipe' : 'Start a room'}</h2>
            <p className="text-ink-muted">
              {who === 'solo'
                ? 'Swipe a deck: right if you would watch it, left if not, up if you have seen it.'
                : 'Share a link, everyone swipes, and the room finds the film you all agree on.'}
            </p>
            <Link to={who === 'solo' ? to('/deck') : to('/rooms')} className={ticketLink}>
              {who === 'solo' ? 'Just swipe' : 'Start a room'}
            </Link>
          </div>
        </TicketCard>

        <TicketCard stubLabel="BY MOOD" stubValue="~">
          <div className="flex flex-col gap-3">
            <h2 className="text-xl font-extrabold tracking-tight">By mood</h2>
            <p className="text-ink-muted">Tell us the vibe and we deal a deck to match.</p>
            <Link to={to('/mood')} className={ticketLink}>
              Pick a mood
            </Link>
          </div>
        </TicketCard>

        <TicketCard stubLabel="BY TASTE" stubValue="♥">
          <div className="flex flex-col gap-3">
            <h2 className="text-xl font-extrabold tracking-tight">By taste</h2>
            <p className="text-ink-muted">Soon: a deck built from what you have loved.</p>
            <span
              aria-disabled="true"
              className="inline-flex min-h-11 items-center justify-center rounded-full border-2 border-ink bg-beige px-5 font-extrabold text-ink-muted"
            >
              Coming soon
            </span>
          </div>
        </TicketCard>
      </div>

      <RoomCodeForm />
    </div>
  )
}
