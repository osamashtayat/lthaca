import { useParams } from 'react-router'
import { ErrorState, LoadingState } from '@/shared/ui'
import { useFilm } from '../api/useFilm'

/**
 * REFERENCE SLICE: a page reads route params, calls a feature api/ hook and renders states.
 * No fetch, no business rules, no direct toast import. Bare on purpose: the real film details screen is a feature task.
 */
export function FilmPage() {
  const tmdbId = Number(useParams().tmdbId)
  const film = useFilm(tmdbId)

  if (film.isPending) return <LoadingState label="Loading film…" />
  if (film.isError) return <ErrorState error={film.error} onRetry={() => void film.refetch()} />

  return (
    <article className="flex flex-col gap-2">
      <p className="font-mono text-xs tracking-[0.12em] text-ink-muted">TMDB {film.data.tmdbId}</p>
      <h1 className="text-3xl font-extrabold tracking-tight">{film.data.title}</h1>
    </article>
  )
}
