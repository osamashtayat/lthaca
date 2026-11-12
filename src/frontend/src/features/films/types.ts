import type { components } from '@/shared/api'

/** Types come from the generated contract (contracts/openapi.json), never hand-written copies of backend DTOs. */
export type Film = components['schemas']['FilmDto']
