import { useInfiniteQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { LibraryShelf, LibrarySort } from '../types'

export const LIBRARY_PAGE_SIZE = 9

export type LibraryFilters = { shelf: LibraryShelf; sort: LibrarySort; genre: string | null }

/** One shelf of the user's library, a page at a time ("load more" = the next page). Filters are part of the key. */
export function useLibrary(filters: LibraryFilters) {
  return useInfiniteQuery({
    queryKey: queryKeys.library.list(filters),
    initialPageParam: 1,
    queryFn: async ({ pageParam }) => {
      const { data } = await api.GET('/api/library', {
        params: {
          query: {
            shelf: filters.shelf,
            sort: filters.sort,
            ...(filters.genre ? { genre: filters.genre } : {}),
            page: pageParam,
            pageSize: LIBRARY_PAGE_SIZE,
          },
        },
      })
      return requireData(data)
    },
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
  })
}
