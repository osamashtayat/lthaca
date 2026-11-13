import type { LibraryShelf, LibrarySort } from './types'

export const SHELVES: readonly { value: LibraryShelf; label: string }[] = [
  { value: 'liked', label: 'Liked' },
  { value: 'watchlist', label: 'Saved' },
  { value: 'seen', label: 'Seen' },
]

export const SORTS: readonly { value: LibrarySort; label: string }[] = [
  { value: 'recent', label: 'Recently added' },
  { value: 'title', label: 'Title A–Z' },
  { value: 'rating', label: 'Your rating' },
]

/** Genre names the filter offers (TMDB's list); the server matches them against each film's genres. */
export const GENRES: readonly string[] = [
  'Action', 'Adventure', 'Animation', 'Comedy', 'Crime', 'Documentary', 'Drama', 'Family', 'Fantasy', 'History',
  'Horror', 'Music', 'Mystery', 'Romance', 'Science Fiction', 'Thriller', 'War', 'Western',
]

const isShelf = (v: string | null): v is LibraryShelf => SHELVES.some((s) => s.value === v)
const isSort = (v: string | null): v is LibrarySort => SORTS.some((s) => s.value === v)

/** Reads the screen's filters from the URL, falling back to the defaults for anything missing or unknown. */
export function filtersFromParams(params: URLSearchParams): { shelf: LibraryShelf; sort: LibrarySort; genre: string | null } {
  const shelf = params.get('shelf')
  const sort = params.get('sort')
  const genre = params.get('genre')
  return {
    shelf: isShelf(shelf) ? shelf : 'liked',
    sort: isSort(sort) ? sort : 'recent',
    genre: genre && GENRES.includes(genre) ? genre : null,
  }
}
