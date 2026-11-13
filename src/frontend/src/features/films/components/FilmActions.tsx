import type { ReactNode } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { isUser, useCurrentUser } from '@/features/auth'
import { useUpdateUserFilm, useUserFilm, type UserFilmState } from '@/features/library'
import { BookmarkIcon, CheckIcon, HeartIcon, MonoLabel, PlayIcon, StubRating, ToggleChip } from '@/shared/ui'

const UNTOUCHED: UserFilmState = { liked: false, onWatchlist: false, seen: false, rating: null }

/**
 * Like / Saved / Seen it / Trailer and "Your rating". Signed-in users change their library state (optimistic PUT);
 * visitors and guests are sent to /sign-in and come back here afterwards.
 */
export function FilmActions({ tmdbId, onTrailer }: { tmdbId: number; onTrailer: () => void }) {
  const session = useCurrentUser()
  const signedIn = isUser(session.data)
  const state = useUserFilm(tmdbId, signedIn)
  const update = useUpdateUserFilm(tmdbId)
  const navigate = useNavigate()
  const location = useLocation()

  const current = state.data ?? UNTOUCHED
  // Until the saved state is known (or while it failed to load) a write would overwrite it blindly.
  const ready = !signedIn || state.isSuccess

  /** Runs `change` for a signed-in user; anyone else goes to /sign-in with this page as the way back. */
  const act = (change: (s: UserFilmState) => UserFilmState) => () => {
    if (!signedIn) {
      void navigate(`/sign-in?next=${encodeURIComponent(`${location.pathname}${location.search}`)}`)
      return
    }
    update.mutate(change(current))
  }

  const toggles: { label: string; icon: ReactNode; on: boolean; change: (s: UserFilmState) => UserFilmState }[] = [
    { label: 'Like', icon: <HeartIcon filled={current.liked} />, on: current.liked, change: (s) => ({ ...s, liked: !s.liked }) },
    { label: 'Saved', icon: <BookmarkIcon filled={current.onWatchlist} />, on: current.onWatchlist, change: (s) => ({ ...s, onWatchlist: !s.onWatchlist }) },
    {
      label: 'Seen it',
      icon: <CheckIcon />,
      on: current.seen,
      // A rating implies seen (api-contract §7), so un-seeing a film also clears its rating.
      change: (s) => ({ ...s, seen: !s.seen, rating: s.seen ? null : s.rating }),
    },
  ]

  return (
    <>
      <div className="flex flex-wrap gap-2">
        {toggles.map((t) => (
          <ToggleChip key={t.label} selected={t.on} disabled={!ready} onClick={act(t.change)} className="gap-1.5 disabled:opacity-50">
            {t.icon}
            {t.label}
          </ToggleChip>
        ))}
        <button
          type="button"
          onClick={onTrailer}
          className="inline-flex min-h-11 items-center gap-1.5 rounded-full border-2 border-ink bg-yellow px-3.5 text-sm font-bold text-ink"
        >
          <PlayIcon />
          Trailer
        </button>
      </div>

      <section className="flex flex-col gap-2">
        <MonoLabel>Your rating</MonoLabel>
        {signedIn ? (
          <StubRating
            label="Your rating"
            value={current.rating}
            disabled={!ready}
            onChange={(rating) => update.mutate({ ...current, seen: true, rating })}
          />
        ) : (
          <button
            type="button"
            onClick={act((s) => s)}
            className="min-h-11 self-start rounded-full border-2 border-dashed border-ink px-4 text-sm font-bold text-ink"
          >
            Sign in to rate it
          </button>
        )}
      </section>
    </>
  )
}
