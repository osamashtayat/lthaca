import type { components } from '@/shared/api'

type FilmDto = components['schemas']['FilmDto']

/**
 * Mock catalog: the same three TMDB ids the backend's Development seed uses (550, 27205, 496243), so links work with
 * mocks on or off. Overviews are our own one-liners, not TMDB text. Posters are null (no network in mocks); backdrop and language
 * stay null like the real API until BE-06 syncs them; votes are filled so the score shows.
 */
export const mockFilms: readonly FilmDto[] = [
  {
    tmdbId: 550,
    title: 'Fight Club',
    releaseYear: 1999,
    overview: 'An insomniac office worker and a soap salesman start an underground club that gets out of hand.',
    posterPath: null,
    runtimeMinutes: 139,
    genres: ['Drama', 'Thriller'],
    backdropPath: null,
    voteAverage: 8.4,
    voteCount: 28000,
    originalLanguage: null,
  },
  {
    tmdbId: 27205,
    title: 'Inception',
    releaseYear: 2010,
    overview: 'A thief who steals secrets from dreams is hired to plant an idea instead.',
    posterPath: null,
    runtimeMinutes: 148,
    genres: ['Action', 'Science Fiction', 'Adventure'],
    backdropPath: null,
    voteAverage: 8.4,
    voteCount: 37000,
    originalLanguage: null,
  },
  {
    tmdbId: 496243,
    title: 'Parasite',
    releaseYear: 2019,
    overview: 'A struggling family works its way, one job at a time, into a wealthy household.',
    posterPath: null,
    runtimeMinutes: 133,
    genres: ['Comedy', 'Thriller', 'Drama'],
    backdropPath: null,
    voteAverage: 8.5,
    voteCount: 19000,
    originalLanguage: null,
  },
]

export function findMockFilm(tmdbId: number): FilmDto | undefined {
  return mockFilms.find((film) => film.tmdbId === tmdbId)
}

/** Mock trailers: a real-looking YouTube key for two films, none for Parasite (exercises the "no trailer" state). */
export const mockTrailers: Readonly<Record<number, { site: 'YouTube'; key: string; name: string }>> = {
  550: { site: 'YouTube', key: 'SUXWAEX2jlg', name: 'Fight Club — Trailer' },
  27205: { site: 'YouTube', key: 'YoHD9XEInc0', name: 'Inception — Trailer' },
}
