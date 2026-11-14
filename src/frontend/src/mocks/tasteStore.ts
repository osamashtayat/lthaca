import type { components } from '@/shared/api'
import { listMockUserFilms } from './libraryStore'

type TasteDto = components['schemas']['TasteDto']

/** A positive signal is a liked film, a rating of 3.5 or more, or a seen film (A16). The server's number is `signalsNeeded`. */
export const MOCK_SIGNALS_NEEDED = 10

let override: TasteDto | null = null

/** Scripts the answer for a test; reset after every test. Without it the mock derives it from the mock library. */
export function setMockTaste(taste: TasteDto | null) {
  override = taste
}

/** Mocks stay dumb (docs/frontend-conventions.md): counts only, no real ranking. */
export function getMockTaste(): TasteDto {
  if (override) return override
  const items = listMockUserFilms()
  const positive = items.filter((i) => i.liked || (i.rating ?? 0) >= 3.5)
  const shares = new Map<string, number>()
  for (const i of positive) for (const g of i.film.genres) shares.set(g, (shares.get(g) ?? 0) + 1)
  const total = [...shares.values()].reduce((a, b) => a + b, 0)
  const ready = positive.length >= MOCK_SIGNALS_NEEDED
  return {
    ready,
    signalCount: positive.length,
    signalsNeeded: MOCK_SIGNALS_NEEDED,
    loggedCount: items.filter((i) => i.seen || i.rating !== null).length,
    topGenres: ready
      ? [...shares]
          .sort((a, b) => b[1] - a[1])
          .slice(0, 3)
          .map(([genre, n]) => ({ genre, percent: Math.max(1, Math.round((n / total) * 100)) }))
      : [],
    notForYou: [],
  }
}

export function resetMockTaste() {
  override = null
}
