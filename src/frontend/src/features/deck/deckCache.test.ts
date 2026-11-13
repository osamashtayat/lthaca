import { describe, expect, it } from 'vitest'
import { applySwipe, appendItems, pendingItems, revertSwipe } from './deckCache'
import type { Deck, DeckItem } from './types'

const film = { tmdbId: 1, title: 'A', releaseYear: 2000, overview: null, posterPath: null, runtimeMinutes: 100, genres: [], backdropPath: null, voteAverage: null, voteCount: null, originalLanguage: null }
const item = (n: number): DeckItem => ({ id: `i${n}`, position: n, film: { ...film, tmdbId: n }, swipe: null, onWatchlist: false })
const deck: Deck = { id: 'd', source: 'justSwipe', createdAt: '', mood: null, items: [item(2), item(1)], swipeCount: 0, yesCount: 0 }

describe('deck cache edits', () => {
  it('applies and reverts a swipe, counting yeses only for right', () => {
    const right = applySwipe(deck, 'i1', 'right', null)
    expect(right).toMatchObject({ swipeCount: 1, yesCount: 1 })
    expect(applySwipe(right, 'i2', 'left', null)).toMatchObject({ swipeCount: 2, yesCount: 1 })
    expect(revertSwipe(right, 'i1')).toEqual(deck)
  })

  it('ignores a second swipe on the same card and unknown cards', () => {
    const once = applySwipe(deck, 'i1', 'left', null)
    expect(applySwipe(once, 'i1', 'right', null)).toBe(once)
    expect(applySwipe(deck, 'nope', 'right', null)).toBe(deck)
    expect(revertSwipe(deck, 'i1')).toBe(deck)
  })

  it('lists pending cards in position order and appends without duplicates', () => {
    expect(pendingItems(deck).map((i) => i.id)).toEqual(['i1', 'i2'])
    expect(appendItems(deck, [item(2), item(3)]).items.map((i) => i.id)).toEqual(['i2', 'i1', 'i3'])
  })
})
