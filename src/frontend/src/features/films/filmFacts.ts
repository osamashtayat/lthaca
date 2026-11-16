import type { Film } from './types'

type FactsFilm = Pick<Film, 'releaseYear' | 'runtimeMinutes' | 'genres' | 'voteAverage'>

/**
 * A film's facts line, as the boards write it: "1999 · 139 min · Drama · ★ 8.4". `genres` is how many to show (0 = none),
 * `score` whether to add TMDB's "★ 8.4". Works for a full film and a list summary alike.
 */
export function filmFacts(film: FactsFilm, { genres = 1, score = true }: { genres?: number; score?: boolean } = {}): string {
  return [
    film.releaseYear,
    film.runtimeMinutes ? `${film.runtimeMinutes} min` : null,
    film.genres.slice(0, genres).join(' / ') || null,
    score && film.voteAverage !== null ? `★ ${film.voteAverage.toFixed(1)}` : null,
  ]
    .filter(Boolean)
    .join(' · ')
}
