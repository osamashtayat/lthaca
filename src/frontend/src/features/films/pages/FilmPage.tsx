import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { notify } from '@/shared/notify'
import { BackIcon, Chip, ErrorState, IconButton, LoadingState, Poster, ShareIcon, tmdbImageUrl } from '@/shared/ui'
import { useFilm } from '../api/useFilm'
import { FilmActions } from '../components/FilmActions'
import { TrailerSheet } from '../components/TrailerSheet'

/** Film details (design board A): backdrop hero, poster, facts, your list and rating, trailer sheet, back and share. */
export function FilmPage() {
  const tmdbId = Number(useParams().tmdbId)
  const film = useFilm(tmdbId)
  const navigate = useNavigate()
  const [trailerOpen, setTrailerOpen] = useState(false)

  if (film.isPending) return <LoadingState label="Loading film…" />
  if (film.isError) return <ErrorState error={film.error} onRetry={() => void film.refetch()} />
  const f = film.data

  const back = () => {
    if (window.history.length > 1) void navigate(-1)
    else void navigate('/')
  }
  const share = async () => {
    const url = window.location.href
    try {
      if (navigator.share) await navigator.share({ title: f.title, url })
      else {
        await navigator.clipboard.writeText(url)
        notify.success('Link copied')
      }
    } catch (error) {
      // Dismissing the share sheet is not an error.
      if (!(error instanceof DOMException && error.name === 'AbortError')) notify.error("Couldn't share this film.")
    }
  }

  const backdrop = tmdbImageUrl(f.backdropPath, 'w780')
  const facts = [f.releaseYear, f.runtimeMinutes ? `${f.runtimeMinutes} min` : null].filter(Boolean).join(' · ')

  return (
    <article className="-mt-6 flex flex-col gap-5 pb-6 lg:mx-auto lg:max-w-3xl">
      <div className="relative -mx-5 h-48 overflow-hidden border-b-2 border-ink bg-beige lg:mx-0 lg:mt-6 lg:rounded-hero lg:border-2">
        {backdrop && <img src={backdrop} alt="" className="size-full object-cover" />}
        <IconButton aria-label="Back" onClick={back} className="absolute top-3 left-4 bg-cream">
          <BackIcon />
        </IconButton>
        <IconButton aria-label="Share" onClick={() => void share()} className="absolute top-3 right-4 bg-cream">
          <ShareIcon />
        </IconButton>
      </div>

      <div className="relative -mt-20 flex items-end gap-4 px-0 lg:px-5">
        <Poster src={tmdbImageUrl(f.posterPath, 'w342')} alt={`${f.title} poster`} className="w-28 shrink-0 shadow-hard" />
        <div className="flex min-w-0 flex-col gap-1.5 pb-1">
          <h1 className="text-3xl leading-none font-extrabold tracking-tight [text-wrap:balance]">{f.title}</h1>
          {facts && <p className="font-mono text-sm text-ink-muted">{facts}</p>}
          {f.voteAverage !== null && (
            <p className="font-mono text-sm">
              <span aria-hidden="true">★ </span>
              <span className="font-medium">{f.voteAverage.toFixed(1)}</span>
              <span className="sr-only"> out of 10</span>
              {f.voteCount !== null && <span className="text-ink-muted"> ({f.voteCount.toLocaleString('en')})</span>}
            </p>
          )}
        </div>
      </div>

      {f.genres.length > 0 && (
        <ul className="flex flex-wrap gap-2" aria-label="Genres">
          {f.genres.map((g) => (
            <li key={g}>
              <Chip>{g}</Chip>
            </li>
          ))}
        </ul>
      )}

      <FilmActions tmdbId={tmdbId} onTrailer={() => setTrailerOpen(true)} />

      {f.overview && <p className="text-base leading-relaxed">{f.overview}</p>}

      <TrailerSheet tmdbId={tmdbId} open={trailerOpen} onClose={() => setTrailerOpen(false)} />
    </article>
  )
}
