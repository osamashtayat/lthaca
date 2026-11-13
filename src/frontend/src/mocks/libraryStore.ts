import type { components } from '@/shared/api'
import { findMockFilm } from './fixtures'

type UserFilmDto = components['schemas']['UserFilmDto']
type UpdateUserFilmRequest = components['schemas']['UpdateUserFilmRequest']

const KEY = 'ithaca.mock.library'
const NOW = '2026-11-13T12:00:00Z'

const store = new Map<number, UpdateUserFilmRequest>(read())

function read(): [number, UpdateUserFilmRequest][] {
  try {
    const raw = globalThis.sessionStorage?.getItem(KEY)
    return raw ? (JSON.parse(raw) as [number, UpdateUserFilmRequest][]) : []
  } catch {
    return []
  }
}

function persist() {
  try {
    // Survives a page reload in the dev server (the mock worker restarts with the page).
    globalThis.sessionStorage?.setItem(KEY, JSON.stringify([...store]))
  } catch {
    // No storage: in-memory is enough.
  }
}

const untouched: UpdateUserFilmRequest = { liked: false, onWatchlist: false, seen: false, rating: null }

/** The caller's state for a film in the catalog (all false / null when untouched), or undefined for an unknown film. */
export function getMockUserFilm(tmdbId: number): UserFilmDto | undefined {
  const film = findMockFilm(tmdbId)
  if (!film) return undefined
  return {
    ...(store.get(tmdbId) ?? untouched),
    film: {
      tmdbId,
      title: film.title,
      releaseYear: film.releaseYear,
      posterPath: film.posterPath,
      runtimeMinutes: film.runtimeMinutes,
      genres: film.genres,
      voteAverage: film.voteAverage,
    },
    updatedAt: NOW,
  }
}

/** PUT semantics (api-contract §7): replaces the state; a rating implies seen; all false + no rating removes it. */
export function putMockUserFilm(tmdbId: number, body: UpdateUserFilmRequest): UserFilmDto | undefined {
  const next = { ...body, seen: body.seen || body.rating !== null }
  if (!next.liked && !next.onWatchlist && !next.seen && next.rating === null) store.delete(tmdbId)
  else store.set(tmdbId, next)
  persist()
  return getMockUserFilm(tmdbId)
}

/** Called from src/test/setup.ts after every test. */
export function resetMockLibrary() {
  store.clear()
  try {
    globalThis.sessionStorage?.removeItem(KEY)
  } catch {
    // ignore
  }
}
