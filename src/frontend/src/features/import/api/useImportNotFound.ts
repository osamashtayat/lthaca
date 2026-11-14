import { useInfiniteQuery } from '@tanstack/react-query'
import { api, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'

export const NOT_FOUND_PAGE_SIZE = 20

/** The films the import couldn't match, a page at a time ("Load more" = the next page). Empty once the list isn't kept. */
export function useImportNotFound(importId: string, enabled = true) {
  return useInfiniteQuery({
    queryKey: queryKeys.imports.notFound(importId),
    initialPageParam: 1,
    enabled,
    queryFn: async ({ pageParam }) => {
      const { data } = await api.GET('/api/imports/{importId}/not-found', {
        params: { path: { importId }, query: { page: pageParam, pageSize: NOT_FOUND_PAGE_SIZE } },
      })
      return requireData(data)
    },
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
  })
}
