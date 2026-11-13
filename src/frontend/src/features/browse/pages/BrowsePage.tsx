import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useGenres } from '@/features/films'
import { Button, ErrorState, LoadingState, MonoLabel, Poster, ToggleChip, tmdbImageUrl } from '@/shared/ui'
import { useBrowseFilms } from '../api/useBrowseFilms'
import { filtersFromParams, hasFilters, MAX_SEARCH_LENGTH, NO_FILTERS, paramsFromFilters, type BrowseFilters } from '../browseFilters'

const SEARCH_DEBOUNCE_MS = 300

/**
 * "Browse": search, quick filters (Under 2h, Hide seen, ★ 7+), genre tiles and the catalog list. With no filters the
 * list is "Trending this week". All filters live in the URL: chips and genres add a history entry (so Back restores the
 * previous filters), typing replaces the current one.
 */
export function BrowsePage() {
  const [params, setParams] = useSearchParams()
  const filters = filtersFromParams(params)
  const genres = useGenres()
  const films = useBrowseFilms(filters)

  function update(next: Partial<BrowseFilters>, replace = false) {
    setParams(paramsFromFilters({ ...filters, ...next }), { replace })
  }

  // The box keeps what you type; the URL (and so the request) follows after a short pause. Back/forward changes the
  // URL, and the box follows it.
  const [text, setText] = useState(filters.search)
  const [urlSearch, setUrlSearch] = useState(filters.search)
  if (filters.search !== urlSearch) {
    // The URL changed (Back, a link, "Clear filters"): the box follows. Adjusting state during render, not in an effect.
    setUrlSearch(filters.search)
    setText(filters.search)
  }
  useEffect(() => {
    if (text === filters.search) return
    const timer = setTimeout(() => update({ search: text }, true), SEARCH_DEBOUNCE_MS)
    return () => clearTimeout(timer)
  })

  const rows = films.data?.pages.flatMap((p) => p.items) ?? []
  const active = hasFilters(filters)

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-5 pb-6">
      <h1 className="text-4xl font-extrabold tracking-tight">Browse</h1>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="browse-search" className="font-mono text-xs tracking-[0.12em] text-ink-muted uppercase">
          Search
        </label>
        <input
          id="browse-search"
          type="search"
          value={text}
          maxLength={MAX_SEARCH_LENGTH}
          placeholder="Title, e.g. Inception"
          onChange={(e) => setText(e.target.value)}
          className="min-h-12 rounded-full border-2 border-ink bg-cream px-5 text-base text-ink"
        />
      </div>

      <div className="flex flex-wrap gap-2">
        <ToggleChip selected={filters.under2h} onClick={() => update({ under2h: !filters.under2h })}>
          Under 2h
        </ToggleChip>
        <ToggleChip selected={filters.hideSeen} onClick={() => update({ hideSeen: !filters.hideSeen })}>
          Hide seen
        </ToggleChip>
        <ToggleChip selected={filters.topRated} onClick={() => update({ topRated: !filters.topRated })}>
          ★ 7+
        </ToggleChip>
      </div>

      {genres.data && genres.data.length > 0 && (
        <div className="flex flex-col gap-2">
          <MonoLabel>Genres</MonoLabel>
          <ul className="grid grid-cols-3 gap-2">
            {genres.data.map((genre) => {
              const selected = filters.genre === genre
              return (
                <li key={genre}>
                  <button
                    type="button"
                    aria-pressed={selected}
                    onClick={() => update({ genre: selected ? null : genre })}
                    className={`flex min-h-12 w-full items-center rounded-tile border-2 border-ink px-3 text-left text-sm leading-tight font-extrabold tracking-tight ${selected ? 'bg-ink text-cream shadow-hard-yellow' : 'bg-cream text-ink'}`}
                  >
                    {genre}
                  </button>
                </li>
              )
            })}
          </ul>
        </div>
      )}

      <h2 className="text-2xl font-extrabold tracking-tight">{active ? 'Results' : 'Trending this week'}</h2>

      {films.isPending ? (
        <LoadingState label="Loading films…" />
      ) : films.isError ? (
        <ErrorState error={films.error} onRetry={() => void films.refetch()} />
      ) : rows.length === 0 ? (
        <div className="flex flex-col items-start gap-3 rounded-card border-2 border-dashed border-ink p-6">
          <p className="text-lg font-extrabold">No films match that.</p>
          <p className="text-ink-muted">Try fewer filters or a different title.</p>
          <Button
            variant="yellow"
            onClick={() => {
              setText('')
              setParams(paramsFromFilters(NO_FILTERS))
            }}
          >
            Clear filters
          </Button>
        </div>
      ) : (
        <>
          <ul className="flex flex-col gap-3">
            {rows.map((film) => (
              <li key={film.tmdbId}>
                <Link
                  to={`/films/${film.tmdbId}`}
                  className="flex gap-4 rounded-tile border-2 border-ink bg-cream p-3 text-ink no-underline"
                >
                  <Poster src={tmdbImageUrl(film.posterPath, 'w342')} alt={`${film.title} poster`} className="w-16 shrink-0" />
                  <div className="flex min-w-0 flex-col gap-1">
                    <span className="text-lg leading-tight font-extrabold tracking-tight">{film.title}</span>
                    <span className="font-mono text-xs text-ink-muted">
                      {[film.releaseYear, film.runtimeMinutes ? `${film.runtimeMinutes} min` : null].filter(Boolean).join(' · ')}
                    </span>
                    {film.voteAverage !== null && (
                      <span className="font-mono text-sm">
                        <span aria-hidden="true">★ </span>
                        {film.voteAverage.toFixed(1)}
                        <span className="sr-only"> out of 10</span>
                      </span>
                    )}
                    <span className="text-sm text-ink-muted">{film.genres.slice(0, 3).join(' · ')}</span>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
          {films.hasNextPage && (
            <Button variant="outline" disabled={films.isFetchingNextPage} onClick={() => void films.fetchNextPage()} className="self-center">
              {films.isFetchingNextPage ? 'Loading…' : 'Load more'}
            </Button>
          )}
        </>
      )}
    </section>
  )
}
