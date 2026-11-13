import type { components } from '@/shared/api'

/** Types come from the generated contract (contracts/openapi.json), never hand-written copies of backend DTOs. */
export type Deck = components['schemas']['DeckDto']
export type DeckItem = components['schemas']['DeckItemDto']
export type DeckTop = components['schemas']['DeckTopDto']
export type SwipeDirection = components['schemas']['SwipeDirection']
export type SeenVerdict = components['schemas']['SeenVerdict']
