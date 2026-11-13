import type { components } from '@/shared/api'

export type RuntimeLimit = components['schemas']['RuntimeLimit']

export const BROWSE_PAGE_SIZE = 8
export const MAX_SEARCH_LENGTH = 100

/** What the Browse screen filters by. Everything is optional; no filters means "Trending this week". */
export type BrowseFilters = {
  search: string
  genre: string | null
  under2h: boolean
  hideSeen: boolean
  topRated: boolean
}

export const NO_FILTERS: BrowseFilters = { search: '', genre: null, under2h: false, hideSeen: false, topRated: false }

/** The URL is the screen's state: `?q=…&genre=…&under2h=1&hideSeen=1&topRated=1`. Missing keys are "off". */
export function filtersFromParams(params: URLSearchParams): BrowseFilters {
  return {
    search: (params.get('q') ?? '').slice(0, MAX_SEARCH_LENGTH),
    genre: params.get('genre') || null,
    under2h: params.get('under2h') === '1',
    hideSeen: params.get('hideSeen') === '1',
    topRated: params.get('topRated') === '1',
  }
}

export function paramsFromFilters(filters: BrowseFilters): URLSearchParams {
  const params = new URLSearchParams()
  if (filters.search) params.set('q', filters.search)
  if (filters.genre) params.set('genre', filters.genre)
  if (filters.under2h) params.set('under2h', '1')
  if (filters.hideSeen) params.set('hideSeen', '1')
  if (filters.topRated) params.set('topRated', '1')
  return params
}

export function hasFilters(filters: BrowseFilters): boolean {
  return filters.search.trim() !== '' || filters.genre !== null || filters.under2h || filters.hideSeen || filters.topRated
}

/** The `GET /api/films` query for the filters (api-contract §9). Sort is always `popular`: "Trending this week". */
export function toFilmsQuery(filters: BrowseFilters, page: number) {
  const search = filters.search.trim()
  return {
    ...(search ? { search } : {}),
    ...(filters.genre ? { genre: filters.genre } : {}),
    ...(filters.under2h ? { maxRuntime: 'upTo2h' as const } : {}),
    ...(filters.topRated ? { minVoteAverage: 7 } : {}),
    ...(filters.hideSeen ? { hideSeen: true } : {}),
    sort: 'popular' as const,
    page,
    pageSize: BROWSE_PAGE_SIZE,
  }
}
