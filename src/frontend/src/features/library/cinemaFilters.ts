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

const isShelf = (v: string | null): v is LibraryShelf => SHELVES.some((s) => s.value === v)
const isSort = (v: string | null): v is LibrarySort => SORTS.some((s) => s.value === v)

/** Reads the screen's filters from the URL, falling back to the defaults for a missing or unknown shelf or sort. */
export function filtersFromParams(params: URLSearchParams): { shelf: LibraryShelf; sort: LibrarySort; genre: string | null } {
  const shelf = params.get('shelf')
  const sort = params.get('sort')
  return {
    shelf: isShelf(shelf) ? shelf : 'liked',
    sort: isSort(sort) ? sort : 'recent',
    // Any name is passed on: the server matches it against each film's genres.
    genre: params.get('genre') || null,
  }
}
