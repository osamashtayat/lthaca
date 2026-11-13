const BASE = 'https://image.tmdb.org/t/p'

/** Full TMDB image URL for a path the API returned (`/abc.jpg`), or null. TMDB images are display only (ADR-0017). */
export function tmdbImageUrl(path: string | null | undefined, size: 'w342' | 'w500' | 'w780' | 'w1280'): string | null {
  return path ? `${BASE}/${size}${path}` : null
}
