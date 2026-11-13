import { describe, expect, it } from 'vitest'
import { filtersFromParams, hasFilters, NO_FILTERS, paramsFromFilters, toFilmsQuery } from './browseFilters'

describe('browse filters', () => {
  it('round-trips through the URL', () => {
    const filters = { search: 'incep', genre: 'Drama', under2h: true, hideSeen: true, topRated: true }
    expect(filtersFromParams(paramsFromFilters(filters))).toEqual(filters)
    expect(paramsFromFilters(NO_FILTERS).toString()).toBe('')
  })

  it('maps filters to the API query (api-contract §9)', () => {
    expect(toFilmsQuery(NO_FILTERS, 1)).toEqual({ sort: 'popular', page: 1, pageSize: 8 })
    expect(toFilmsQuery({ search: '  incep ', genre: 'Drama', under2h: true, hideSeen: true, topRated: true }, 2)).toEqual({
      search: 'incep', genre: 'Drama', maxRuntime: 'upTo2h', minVoteAverage: 7, hideSeen: true, sort: 'popular', page: 2, pageSize: 8,
    })
  })

  it('knows when any filter is on', () => {
    expect(hasFilters(NO_FILTERS)).toBe(false)
    expect(hasFilters({ ...NO_FILTERS, search: '   ' })).toBe(false)
    expect(hasFilters({ ...NO_FILTERS, genre: 'Drama' })).toBe(true)
  })

  it('caps a long search from the URL', () => {
    expect(filtersFromParams(new URLSearchParams({ q: 'x'.repeat(300) })).search).toHaveLength(100)
  })
})
