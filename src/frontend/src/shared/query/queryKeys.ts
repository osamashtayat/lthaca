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
    trailer: (tmdbId: number) => ['films', 'trailer', tmdbId] as const,
  },
  library: {
    all: ['library'] as const,
    detail: (tmdbId: number) => ['library', 'detail', tmdbId] as const,
  },
} as const
