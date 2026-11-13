import { http, HttpResponse } from 'msw'
import { addMockAccount, findMockAccount, getMockSession, isMockEmailTaken, setMockSession } from './authStore'
import { findMockFilm, mockTrailers } from './fixtures'
import { createMockDeck, mockDeckDto, parseMockMood, mockDeckTop, nextMockBatch, recordMockSwipe } from './deckStore'
import { getMockUserFilm, putMockUserFilm, setMockWatchlist } from './libraryStore'
import { apiPath, problem, type RequestOf, type ResponseOf } from './http'

/**
 * Mock API (ADR-0020): one block per contract area, shared by the dev server (browser.ts) and Vitest (server.ts).
 * How to add one: docs/frontend-conventions.md → Mock API.
 */

const healthHandlers = [
  http.get(apiPath('/api/health'), () => HttpResponse.json<ResponseOf<'/api/health', 'get', 200>>({ status: 'ok' })),
]

const filmHandlers = [
  http.get(apiPath('/api/films/{tmdbId}'), ({ params }) => {
    const film = findMockFilm(Number(params.tmdbId))
    return film ? HttpResponse.json<ResponseOf<'/api/films/{tmdbId}', 'get', 200>>(film) : problem(404, 'film.not_found')
  }),
  http.get(apiPath('/api/films/{tmdbId}/trailer'), ({ params }) => {
    const tmdbId = Number(params.tmdbId)
    if (!findMockFilm(tmdbId)) return problem(404, 'film.not_found')
    return HttpResponse.json<ResponseOf<'/api/films/{tmdbId}/trailer', 'get', 200>>({ trailer: mockTrailers[tmdbId] ?? null })
  }),
]

type CurrentUser = ResponseOf<'/api/auth/me', 'get', 200>

/** The few rules the screens need to be exercised: field validation, one taken email, one wrong-password path. */
function credentialErrors(email: string, password: string): Record<string, string[]> {
  const errors: Record<string, string[]> = {}
  if (!email.includes('@')) errors.email = ['Enter a valid email address.']
  if (password.length < 8 || password.length > 128) errors.password = ['Use 8 to 128 characters.']
  return errors
}

const authHandlers = [
  http.get(apiPath('/api/auth/me'), () => {
    const session = getMockSession()
    return session ? HttpResponse.json<CurrentUser>(session) : problem(401, 'auth.unauthenticated')
  }),
  http.post(apiPath('/api/auth/register'), async ({ request }) => {
    const body = (await request.json()) as RequestOf<'/api/auth/register', 'post'>
    const errors = credentialErrors(body.email, body.password)
    const name = body.displayName.trim()
    if (name.length < 1 || name.length > 40) errors.displayName = ['Use 1 to 40 characters.']
    if (Object.keys(errors).length > 0) return problem(400, 'validation', errors)
    if (isMockEmailTaken(body.email)) return problem(409, 'auth.email_taken')
    const user = addMockAccount(body.email, body.password, name)
    setMockSession(user)
    return HttpResponse.json<ResponseOf<'/api/auth/register', 'post', 201>>(user, { status: 201 })
  }),
  http.post(apiPath('/api/auth/sign-in'), async ({ request }) => {
    const body = (await request.json()) as RequestOf<'/api/auth/sign-in', 'post'>
    const errors = credentialErrors(body.email, body.password)
    if (Object.keys(errors).length > 0) return problem(400, 'validation', errors)
    const user = findMockAccount(body.email, body.password)
    if (!user) return problem(401, 'auth.invalid_credentials')
    setMockSession(user)
    return HttpResponse.json<ResponseOf<'/api/auth/sign-in', 'post', 200>>(user)
  }),
  http.post(apiPath('/api/auth/sign-out'), () => {
    setMockSession(null)
    return new HttpResponse(null, { status: 204 })
  }),
]

const libraryHandlers = [
  http.get(apiPath('/api/library/{tmdbId}'), ({ params }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const item = getMockUserFilm(Number(params.tmdbId))
    return item ? HttpResponse.json<ResponseOf<'/api/library/{tmdbId}', 'get', 200>>(item) : problem(404, 'film.not_found')
  }),
  http.put(apiPath('/api/library/{tmdbId}'), async ({ params, request }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const body = (await request.json()) as RequestOf<'/api/library/{tmdbId}', 'put'>
    if (body.rating !== null && (body.rating < 1 || body.rating > 5)) return problem(400, 'validation', { rating: ['Use 1 to 5.'] })
    const item = putMockUserFilm(Number(params.tmdbId), body)
    return item ? HttpResponse.json<ResponseOf<'/api/library/{tmdbId}', 'put', 200>>(item) : problem(404, 'film.not_found')
  }),
]

const watchlistHandlers = [
  http.put(apiPath('/api/library/{tmdbId}/watchlist'), ({ params }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    if (!findMockFilm(Number(params.tmdbId))) return problem(404, 'film.not_found')
    setMockWatchlist(Number(params.tmdbId), true)
    return new HttpResponse(null, { status: 204 })
  }),
  http.delete(apiPath('/api/library/{tmdbId}/watchlist'), ({ params }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    if (!findMockFilm(Number(params.tmdbId))) return problem(404, 'film.not_found')
    setMockWatchlist(Number(params.tmdbId), false)
    return new HttpResponse(null, { status: 204 })
  }),
]

/** Solo decks (api-contract §5): "Just swipe" and "By mood" (scripted mood-parse outcomes, see parseMockMood). State in deckStore.ts. */
const deckHandlers = [
  http.post(apiPath('/api/decks'), async ({ request }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const body = (await request.json()) as RequestOf<'/api/decks', 'post'>
    if ((body.source === 'mood') !== (body.mood !== null)) return problem(400, 'validation', { mood: ['Required with source "mood" only.'] })
    let mood = null
    if (body.mood) {
      const outcome = parseMockMood(body.mood)
      if (outcome.kind === 'error') {
        return problem(outcome.status, outcome.code, outcome.code === 'validation' ? { mood: ['Check the moods and text.'] } : undefined)
      }
      mood = outcome.mood
    }
    const deck = createMockDeck(mood)
    return HttpResponse.json<ResponseOf<'/api/decks', 'post', 201>>(deck, {
      status: 201,
      headers: { Location: `/api/decks/${deck.id}` },
    })
  }),
  http.get(apiPath('/api/decks/{deckId}'), ({ params }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const deck = mockDeckDto(String(params.deckId))
    return deck ? HttpResponse.json<ResponseOf<'/api/decks/{deckId}', 'get', 200>>(deck) : problem(404, 'deck.not_found')
  }),
  http.post(apiPath('/api/decks/{deckId}/items'), ({ params }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const items = nextMockBatch(String(params.deckId))
    return items ? HttpResponse.json<ResponseOf<'/api/decks/{deckId}/items', 'post', 200>>(items) : problem(404, 'deck.not_found')
  }),
  http.post(apiPath('/api/decks/{deckId}/swipes'), async ({ params, request }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const body = (await request.json()) as RequestOf<'/api/decks/{deckId}/swipes', 'post'>
    if (body.seenVerdict !== null && body.direction !== 'up') {
      return problem(400, 'validation', { seenVerdict: ['Only with an up swipe.'] })
    }
    const outcome = recordMockSwipe(String(params.deckId), body)
    if (outcome === 'deck_not_found') return problem(404, 'deck.not_found')
    if (outcome === 'item_not_found') return problem(404, 'deck.item_not_found')
    if (outcome === 'already_swiped') return problem(409, 'deck.already_swiped')
    return new HttpResponse(null, { status: 204 })
  }),
  http.get(apiPath('/api/decks/{deckId}/top'), ({ params }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const top = mockDeckTop(String(params.deckId))
    if (top === undefined) return problem(404, 'deck.not_found')
    if (top === null) return problem(422, 'deck.no_yeses')
    return HttpResponse.json<ResponseOf<'/api/decks/{deckId}/top', 'get', 200>>(top)
  }),
]

export const handlers = [...healthHandlers, ...authHandlers, ...filmHandlers, ...libraryHandlers, ...watchlistHandlers, ...deckHandlers]
