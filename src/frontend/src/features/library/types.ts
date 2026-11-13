import type { components } from '@/shared/api'

/** Types for the library feature. API shapes come from @/shared/api (generated), not hand-written. */
export type UserFilm = components['schemas']['UserFilmDto']
export type UserFilmState = components['schemas']['UpdateUserFilmRequest']
