/**
 * Central TanStack Query keys. Hooks build keys here only, so invalidation by prefix is predictable:
 * `queryKeys.films.all` invalidates every film query. Add a block per feature as it lands.
 */
/** Matches every cached query that isn't the session itself: what must be dropped or refetched when the principal changes. */
export const isNotSessionQuery = (query: { queryKey: readonly unknown[] }) => query.queryKey[0] !== 'auth'

export const queryKeys = {
  health: ['health'] as const,
  auth: {
    all: ['auth'] as const,
    me: ['auth', 'me'] as const,
  },
  films: {
    all: ['films'] as const,
    detail: (tmdbId: number) => ['films', 'detail', tmdbId] as const,
    genres: ['films', 'genres'] as const,
    trailer: (tmdbId: number) => ['films', 'trailer', tmdbId] as const,
  },
  decks: {
    all: ['decks'] as const,
    detail: (deckId: string) => ['decks', 'detail', deckId] as const,
    top: (deckId: string) => ['decks', 'top', deckId] as const,
  },
  browse: {
    all: ['browse'] as const,
    list: (query: object) => ['browse', 'list', query] as const,
  },
  rooms: {
    all: ['rooms'] as const,
    // deck / progress / result sit under `detail`, so invalidating `detail(code)` (no `exact`) refetches the whole room.
    detail: (code: string) => ['rooms', 'detail', code] as const,
    deck: (code: string) => ['rooms', 'detail', code, 'deck'] as const,
    progress: (code: string) => ['rooms', 'detail', code, 'progress'] as const,
    result: (code: string) => ['rooms', 'detail', code, 'result'] as const,
    preview: (code: string) => ['rooms', 'preview', code] as const,
  },
  library: {
    all: ['library'] as const,
    detail: (tmdbId: number) => ['library', 'detail', tmdbId] as const,
    list: (filters: { shelf: string; sort: string; genre: string | null }) => ['library', 'list', filters] as const,
    summary: ['library', 'summary'] as const,
  },
} as const
