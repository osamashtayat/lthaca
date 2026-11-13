import { http } from 'msw'
import { apiPath, problem } from './http'

/** Ready-made failures for `server.use(...)` in tests, so test files need no `msw` import (ADR-0020). */
export const failLibraryUpdate = () => http.put(apiPath('/api/library/{tmdbId}'), () => problem(500, 'server.error'))
export const failDeckSwipe = () => http.post(apiPath('/api/decks/{deckId}/swipes'), () => problem(500, 'server.error'))
export const slowUnavailableDeckCreate = (ms: number) =>
  http.post(apiPath('/api/decks'), async () => {
    await new Promise((resolve) => setTimeout(resolve, ms))
    return problem(503, 'ai.unavailable')
  })
export const notHostStart = () => http.post(apiPath('/api/rooms/{code}/start'), () => problem(403, 'room.not_host'))
