/** Public surface of the library feature: the My Cinema screen and the hooks for the user's state on a film. */
export { CinemaPage } from './pages/CinemaPage'
export { useUserFilm } from './api/useUserFilm'
export { useWatchlist } from './api/useWatchlist'
export { useUpdateUserFilm } from './api/useUpdateUserFilm'
export type { UserFilm, UserFilmState } from './types'
