import type { components } from '@/shared/api'

/** The taste summary and readiness (api-contract §12): the server decides `ready`, the screen only shows it. */
export type Taste = components['schemas']['TasteDto']
export type TasteGenre = components['schemas']['TasteGenreDto']
