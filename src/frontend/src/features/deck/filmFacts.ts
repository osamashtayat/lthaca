import type { Film } from '@/features/films'

/**
 * The facts line, as on the deck board: "1999 · 139 min · Drama · ★ 8.4". The card fits one genre on a phone; the story
 * sheet shows two.
 */
export function filmFacts(film: Film, genres = 1): string {
  return [
    film.releaseYear,
    film.runtimeMinutes ? `${film.runtimeMinutes} min` : null,
    film.genres.slice(0, genres).join(' / ') || null,
    film.voteAverage !== null && film.voteAverage !== undefined ? `★ ${film.voteAverage.toFixed(1)}` : null,
  ]
    .filter(Boolean)
    .join(' · ')
}
