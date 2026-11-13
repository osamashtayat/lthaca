import { Link, useSearchParams } from 'react-router'
import { useGenres } from '@/features/films'
import { Button, ErrorState, LoadingState, MonoLabel, Poster, SegmentedControl, tmdbImageUrl } from '@/shared/ui'
import { useLibrary } from '../api/useLibrary'
import { useLibrarySummary } from '../api/useLibrarySummary'
import { filtersFromParams, SHELVES, SORTS } from '../cinemaFilters'
import type { LibrarySort, LibraryShelf } from '../types'

/** "My Cinema": the shelves (Liked / Watchlist / Seen) with counts, genre filter, sort, poster grid and load more. Filters live in the URL. */
export function CinemaPage() {
  const [params, setParams] = useSearchParams()
  const filters = filtersFromParams(params)
  const summary = useLibrarySummary()
  const library = useLibrary(filters)
  const genres = useGenres().data ?? []

  function update(next: Partial<typeof filters>) {
    const merged = { ...filters, ...next }
    const search = new URLSearchParams()
    if (merged.shelf !== 'liked') search.set('shelf', merged.shelf)
    if (merged.sort !== 'recent') search.set('sort', merged.sort)
    if (merged.genre) search.set('genre', merged.genre)
    setParams(search, { replace: true })
  }

  const shelfOptions = SHELVES.map((s) => ({
    value: s.value,
    label: summary.data ? `${s.label} ${summary.data[s.value]}` : s.label,
  }))
  const items = library.data?.pages.flatMap((p) => p.items) ?? []

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-5 pb-6">
      <div className="flex flex-wrap items-end justify-between gap-x-3">
        <h1 className="text-4xl font-extrabold tracking-tight">My Cinema</h1>
        <Link to="/import" className="inline-flex min-h-11 items-center font-bold text-pink-text">
          Import from Letterboxd
        </Link>
      </div>

      <SegmentedControl label="Shelf" options={shelfOptions} value={filters.shelf} onChange={(shelf: LibraryShelf) => update({ shelf })} />

      <div className="flex gap-3">
        <label className="flex flex-1 flex-col gap-1.5">
          <MonoLabel>Genre</MonoLabel>
          <select
            value={filters.genre ?? ''}
            onChange={(e) => update({ genre: e.target.value === '' ? null : e.target.value })}
            className="min-h-12 rounded-control border-2 border-ink bg-cream px-3 text-base text-ink"
          >
            <option value="">All genres</option>
            {genres.map((g) => (
              <option key={g} value={g}>
                {g}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-1 flex-col gap-1.5">
          <MonoLabel>Sort</MonoLabel>
          <select
            value={filters.sort}
            onChange={(e) => update({ sort: e.target.value as LibrarySort })}
            className="min-h-12 rounded-control border-2 border-ink bg-cream px-3 text-base text-ink"
          >
            {SORTS.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      {library.isPending ? (
        <LoadingState label="Loading your shelf…" />
      ) : library.isError ? (
        <ErrorState error={library.error} onRetry={() => void library.refetch()} />
      ) : items.length === 0 ? (
        <div className="flex flex-col items-start gap-3 rounded-card border-2 border-dashed border-ink p-6">
          <h2 className="text-2xl font-extrabold tracking-tight">Nothing on the reel yet</h2>
          <p className="text-ink-muted">
            {filters.genre ? 'No films on this shelf for that genre.' : 'Films you like, save or mark as seen land here.'}
          </p>
          <Link to="/deck" className="inline-flex min-h-11 items-center rounded-full border-2 border-ink bg-yellow px-5 font-extrabold text-ink no-underline">
            Start swiping
          </Link>
        </div>
      ) : (
        <>
          <ul className="grid grid-cols-3 gap-3" aria-label={`${SHELVES.find((s) => s.value === filters.shelf)?.label ?? ''} films`}>
            {items.map(({ film, rating }) => (
              <li key={film.tmdbId}>
                <Link to={`/films/${film.tmdbId}`} className="flex flex-col gap-1.5 text-ink no-underline">
                  <Poster src={tmdbImageUrl(film.posterPath, 'w342')} alt={`${film.title} poster`} className="w-full" />
                  <span className="text-sm leading-tight font-bold">{film.title}</span>
                  {rating !== null && <span className="font-mono text-xs text-ink-muted">YOU · {rating}/5</span>}
                </Link>
              </li>
            ))}
          </ul>
          {library.hasNextPage && (
            <Button variant="outline" disabled={library.isFetchingNextPage} onClick={() => void library.fetchNextPage()} className="self-center">
              {library.isFetchingNextPage ? 'Loading…' : 'Load more'}
            </Button>
          )}
        </>
      )}
    </section>
  )
}
