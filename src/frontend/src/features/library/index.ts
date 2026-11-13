/** Public surface of the library feature: hooks for the signed-in user's state on a film. Pages arrive with FE-07. */
export { useUserFilm } from './api/useUserFilm'
export { useWatchlist } from './api/useWatchlist'
export { useUpdateUserFilm } from './api/useUpdateUserFilm'
export type { UserFilm, UserFilmState } from './types'
