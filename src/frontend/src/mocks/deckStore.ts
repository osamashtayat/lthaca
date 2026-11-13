import type { components } from '@/shared/api'
import { mockFilms } from './fixtures'
import { isOnMockWatchlist } from './libraryStore'

type DeckDto = components['schemas']['DeckDto']
type DeckItemDto = components['schemas']['DeckItemDto']
type SwipeRequest = components['schemas']['SwipeRequest']
type DeckTopDto = components['schemas']['DeckTopDto']
type MoodInput = components['schemas']['MoodInput']
type MoodQueryDto = components['schemas']['MoodQueryDto']

/** Cards per batch. Small on purpose: the mock catalog has 12 films, so a deck is three batches (5 + 5 + 2), then `[]`. */
export const MOCK_BATCH_SIZE = 5

type StoredDeck = { id: string; createdAt: string; mood: MoodQueryDto | null; served: number; swipes: Map<string, SwipeRequest> /* by deckItemId */; swipeIds: Set<string> }
const KEY = 'ithaca.mock.decks'
type Saved = [string, Omit<StoredDeck, 'swipes' | 'swipeIds'> & { swipes: [string, SwipeRequest][]; swipeIds: string[] }][]

// Survives a page reload in the dev server (the mock worker restarts with the page), like the other mock stores.
function load(): Map<string, StoredDeck> {
  try {
    const raw = globalThis.sessionStorage?.getItem(KEY)
    const saved = raw ? (JSON.parse(raw) as Saved) : []
    return new Map(saved.map(([id, d]) => [id, { ...d, swipes: new Map(d.swipes), swipeIds: new Set(d.swipeIds) }]))
  } catch {
    return new Map()
  }
}

function persist() {
  try {
    const saved: Saved = [...decks].map(([id, d]) => [id, { ...d, swipes: [...d.swipes], swipeIds: [...d.swipeIds] }])
    globalThis.sessionStorage?.setItem(KEY, JSON.stringify(saved))
  } catch {
    // No storage: in-memory is enough.
  }
}

const decks = load()

const itemId = (deckId: string, position: number) => `${deckId.slice(0, 24)}${String(position).padStart(12, '0')}`

function itemAt(deck: StoredDeck, position: number): DeckItemDto {
  const film = mockFilms[position - 1]
  if (!film) throw new Error(`no mock film at position ${position}`)
  const swipe = deck.swipes.get(itemId(deck.id, position))
  return {
    id: itemId(deck.id, position),
    position,
    film,
    swipe: swipe ? { direction: swipe.direction, seenVerdict: swipe.seenVerdict } : null,
    onWatchlist: isOnMockWatchlist(film.tmdbId),
  }
}

export function createMockDeck(mood: MoodQueryDto | null = null): DeckDto {
  const id = crypto.randomUUID()
  const deck: StoredDeck = { id, createdAt: '2026-11-13T12:00:00Z', mood, served: 0, swipes: new Map(), swipeIds: new Set() }
  decks.set(id, deck)
  nextMockBatch(id)
  return mockDeckDto(id) as DeckDto
}

export function mockDeckDto(deckId: string): DeckDto | undefined {
  const deck = decks.get(deckId)
  if (!deck) return undefined
  const swipes = [...deck.swipes.values()]
  return {
    id: deck.id,
    source: deck.mood ? 'mood' : 'justSwipe',
    createdAt: deck.createdAt,
    mood: deck.mood,
    items: Array.from({ length: deck.served }, (_, i) => itemAt(deck, i + 1)),
    swipeCount: swipes.length,
    yesCount: swipes.filter((s) => s.direction === 'right').length,
  }
}

/** The next batch of cards; `[]` once the catalog is used up. `undefined` for an unknown deck. */
export function nextMockBatch(deckId: string): DeckItemDto[] | undefined {
  const deck = decks.get(deckId)
  if (!deck) return undefined
  const from = deck.served + 1
  const to = Math.min(deck.served + MOCK_BATCH_SIZE, mockFilms.length)
  deck.served = Math.max(deck.served, to)
  persist()
  return Array.from({ length: Math.max(0, to - from + 1) }, (_, i) => itemAt(deck, from + i))
}

export type SwipeOutcome = 'ok' | 'deck_not_found' | 'item_not_found' | 'already_swiped'

/** Records a swipe. Repeating the same `swipeId` is a no-op success (idempotent); a different one for a swiped card is a 409. */
export function recordMockSwipe(deckId: string, request: SwipeRequest): SwipeOutcome {
  const deck = decks.get(deckId)
  if (!deck) return 'deck_not_found'
  if (deck.swipeIds.has(request.swipeId)) return 'ok'
  const known = Array.from({ length: deck.served }, (_, i) => itemId(deck.id, i + 1))
  if (!known.includes(request.deckItemId)) return 'item_not_found'
  if (deck.swipes.has(request.deckItemId)) return 'already_swiped'
  deck.swipes.set(request.deckItemId, request)
  deck.swipeIds.add(request.swipeId)
  persist()
  return 'ok'
}

/** Up to three right-swiped films, in swipe order (the real ranking is the server's). `null` when there are none. */
export function mockDeckTop(deckId: string): DeckTopDto | null | undefined {
  const dto = mockDeckDto(deckId)
  if (!dto) return undefined
  const yes = dto.items.filter((i) => i.swipe?.direction === 'right')
  if (yes.length === 0) return null
  return {
    swipeCount: dto.swipeCount,
    yesCount: dto.yesCount,
    picks: yes.slice(0, 3).map((i) => ({ film: i.film, reason: null })),
  }
}

/** Called from src/test/setup.ts after every test. */
export function resetMockDecks() {
  decks.clear()
  try {
    globalThis.sessionStorage?.removeItem(KEY)
  } catch {
    // ignore
  }
}

const RUNTIME_MINUTES = { under90: 90, upTo2h: 120, any: null } as const

export type MockMoodOutcome =
  | { kind: 'ok'; mood: MoodQueryDto }
  | { kind: 'error'; status: 400 | 422 | 502 | 503; code: 'validation' | 'mood.unclear' | 'ai.refused' | 'ai.invalid_output' | 'ai.unavailable' }

/**
 * Stands in for mood-parse (ADR-0022) with scripted outcomes, so every state of the screen can be reached by typing a
 * keyword: "refuse" → ai.refused, "invalid" → ai.invalid_output, "offline" → ai.unavailable, "asdf" → the text isn't a
 * mood (422 mood.unclear without chips; with chips the deck is built from them and `textIgnored` is true). Anything
 * else is a usable mood. Empty text makes no "LLM call".
 */
// The limits (2 moods, 280 characters) mirror api-contract §5; mocks can't import them from the mood feature.
export function parseMockMood(input: MoodInput): MockMoodOutcome {
  const text = input.text?.trim() ?? ''
  if (input.text !== null && input.text.length > 280) return { kind: 'error', status: 400, code: 'validation' }
  if (input.moods.length > 2) return { kind: 'error', status: 400, code: 'validation' }
  const lower = text.toLowerCase()
  if (lower.includes('refuse')) return { kind: 'error', status: 422, code: 'ai.refused' }
  if (lower.includes('invalid')) return { kind: 'error', status: 502, code: 'ai.invalid_output' }
  if (lower.includes('offline')) return { kind: 'error', status: 503, code: 'ai.unavailable' }
  const unclear = lower.includes('asdf')
  if (unclear && input.moods.length === 0) return { kind: 'error', status: 422, code: 'mood.unclear' }
  return {
    kind: 'ok',
    mood: {
      id: crypto.randomUUID(),
      ...input,
      text: text === '' ? null : input.text,
      filters: { includeGenres: [], excludeGenres: [], maxRuntimeMinutes: RUNTIME_MINUTES[input.maxRuntime], minReleaseYear: null, maxReleaseYear: null, keywords: [] },
      textIgnored: unclear,
    },
  }
}
