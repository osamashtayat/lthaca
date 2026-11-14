import { useState } from 'react'
import { SessionAction } from '@/features/auth'
import { ErrorState, LoadingState, MonoLabel, TicketCard, Button } from '@/shared/ui'
import { useProfile } from '../api/useProfile'
import { ChangePasswordSheet } from '../components/ChangePasswordSheet'
import { DeleteAccountSheet } from '../components/DeleteAccountSheet'
import { DisplayNameForm } from '../components/DisplayNameForm'
import { LetterboxdRow } from '../components/LetterboxdRow'
import { TasteSummary } from '../components/TasteSummary'
import { formatSince, initialOf } from '../format'

/** `/profile` (user-only): who you are, your name, taste, the Letterboxd entry, password, sign out and account deletion. */
export function ProfilePage() {
  const profile = useProfile()
  const [sheet, setSheet] = useState<'password' | 'delete' | null>(null)

  if (profile.isPending) return <LoadingState label="Loading…" />
  if (profile.isError) return <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />
  const p = profile.data

  return (
    <section className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6">
      <h1 className="text-4xl font-extrabold tracking-tight">Profile &amp; settings</h1>

      <TicketCard stubLabel={`SINCE ${formatSince(p.createdAt).toUpperCase()}`} stubValue={initialOf(p.displayName)}>
        <div className="flex flex-col gap-1">
          <h2 className="text-2xl font-extrabold tracking-tight">{p.displayName}</h2>
          <p className="text-ink-muted">{p.email}</p>
        </div>
      </TicketCard>

      <DisplayNameForm key={p.displayName} displayName={p.displayName} />
      <TasteSummary />
      <LetterboxdRow />

      <div className="flex flex-col gap-3">
        <MonoLabel>Account</MonoLabel>
        <Button variant="outline" onClick={() => setSheet('password')} className="w-full">
          Change password
        </Button>
        <SessionAction className="w-full" />
        <Button variant="outline" onClick={() => setSheet('delete')} className="w-full text-pink-text">
          Delete account
        </Button>
      </div>

      <ChangePasswordSheet open={sheet === 'password'} onClose={() => setSheet(null)} />
      <DeleteAccountSheet open={sheet === 'delete'} onClose={() => setSheet(null)} />
    </section>
  )
}
