/**
 * Central TanStack Query keys. Hooks build keys here only, so invalidation by prefix is predictable:
 * `queryKeys.films.all` invalidates every film query. Add a block per feature as it lands.
 */
export const queryKeys = {
  health: ['health'] as const,
  films: {
    all: ['films'] as const,
    detail: (tmdbId: number) => ['films', 'detail', tmdbId] as const,
  },
} as const
