import { describe, expect, it } from 'vitest'
import { filtersFromParams } from './cinemaFilters'

describe('filtersFromParams', () => {
  it('defaults when the URL is empty', () => {
    expect(filtersFromParams(new URLSearchParams())).toEqual({ shelf: 'liked', sort: 'recent', genre: null })
  })
  it('reads known values and drops unknown ones', () => {
    expect(filtersFromParams(new URLSearchParams('shelf=seen&sort=title&genre=Drama'))).toEqual({ shelf: 'seen', sort: 'title', genre: 'Drama' })
    expect(filtersFromParams(new URLSearchParams('shelf=nope&sort=x&genre=Nope'))).toEqual({ shelf: 'liked', sort: 'recent', genre: null })
  })
})
