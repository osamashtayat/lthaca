import { useInfiniteQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import { toFilmsQuery, type BrowseFilters } from '../browseFilters'

/** The catalog list for the Browse filters, a page at a time ("load more" = the next page). */
export function useBrowseFilms(filters: BrowseFilters) {
  return useInfiniteQuery({
    queryKey: queryKeys.browse.list(toFilmsQuery(filters, 0)),
    initialPageParam: 1,
    queryFn: async ({ pageParam }) => {
      const { data } = await api.GET('/api/films', { params: { query: toFilmsQuery(filters, pageParam) } })
      return requireData(data)
    },
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
  })
}
