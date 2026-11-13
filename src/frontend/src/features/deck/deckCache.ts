import type { Deck, DeckItem, SeenVerdict, SwipeDirection } from './types'

/** Pure edits of the cached deck, so a swipe shows at once and can be taken back exactly (see useSwipe). */
export function applySwipe(deck: Deck, itemId: string, direction: SwipeDirection, seenVerdict: SeenVerdict | null): Deck {
  const item = deck.items.find((i) => i.id === itemId)
  if (!item || item.swipe) return deck
  return {
    ...deck,
    items: deck.items.map((i) => (i.id === itemId ? { ...i, swipe: { direction, seenVerdict } } : i)),
    swipeCount: deck.swipeCount + 1,
    yesCount: deck.yesCount + (direction === 'right' ? 1 : 0),
  }
}

export function revertSwipe(deck: Deck, itemId: string): Deck {
  const item = deck.items.find((i) => i.id === itemId)
  if (!item?.swipe) return deck
  return {
    ...deck,
    items: deck.items.map((i) => (i.id === itemId ? { ...i, swipe: null } : i)),
    swipeCount: deck.swipeCount - 1,
    yesCount: deck.yesCount - (item.swipe.direction === 'right' ? 1 : 0),
  }
}

/** Appends a fetched batch, skipping items we already have. */
export function appendItems(deck: Deck, items: DeckItem[]): Deck {
  const known = new Set(deck.items.map((i) => i.id))
  return { ...deck, items: [...deck.items, ...items.filter((i) => !known.has(i.id))] }
}

/** The cards still to swipe, in position order. */
export function pendingItems(deck: Deck): DeckItem[] {
  return deck.items.filter((i) => i.swipe === null).sort((a, b) => a.position - b.position)
}
