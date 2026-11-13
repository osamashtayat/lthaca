import { http, HttpResponse } from 'msw'
import { addMockAccount, findMockAccount, getMockSession, isMockEmailTaken, setMockSession } from './authStore'
import { findMockFilm, mockFilms, mockTrailers } from './fixtures'
import { createMockDeck, mockDeckDto, parseMockMood, mockDeckTop, nextMockBatch, recordMockSwipe } from './deckStore'
import { addMockRoomMember, createMockRoom, findMockRoom, mockRoomFor, removeMockRoomMember, setMockRoomStatus, MOCK_MAX_MEMBERS } from './roomStore'
import { getMockUserFilm, listMockUserFilms, putMockUserFilm, setMockWatchlist } from './libraryStore'
import { apiPath, problem, type RequestOf, type ResponseOf } from './http'

/**
 * Mock API (ADR-0020): one block per contract area, shared by the dev server (browser.ts) and Vitest (server.ts).
 * How to add one: docs/frontend-conventions.md → Mock API.
 */

const healthHandlers = [
  http.get(apiPath('/api/health'), () => HttpResponse.json<ResponseOf<'/api/health', 'get', 200>>({ status: 'ok' })),
]

const filmHandlers = [
  // Before `/api/films/{tmdbId}`, or "genres" would be taken for an id.
  http.get(apiPath('/api/films/genres'), () =>
    HttpResponse.json<ResponseOf<'/api/films/genres', 'get', 200>>([...new Set(mockFilms.flatMap((f) => f.genres))].sort()),
  ),
  http.get(apiPath('/api/films'), ({ request }) => {
    const q = new URL(request.url).searchParams
    const page = Number(q.get('page') ?? 1)
    const pageSize = Number(q.get('pageSize') ?? 20)
    const sort = q.get('sort') ?? 'popular'
    const search = q.get('search')?.toLowerCase() ?? ''
    if (page < 1 || pageSize < 1 || pageSize > 100 || search.length > 100 || !['popular', 'newest', 'rating'].includes(sort)) {
      return problem(400, 'validation', { query: ['Check search, sort, page and pageSize.'] })
    }
    const limit = { under90: 90, upTo2h: 120 }[q.get('maxRuntime') ?? ''] ?? Infinity
    const genre = q.get('genre')
    // Catalog order stands in for TMDB popularity. "Hide seen" needs a user, like the real API.
    const seen =
      q.get('hideSeen') === 'true' && getMockSession()
        ? new Set(listMockUserFilms().filter((i) => i.seen).map((i) => i.film.tmdbId))
        : new Set<number>()
    const matches = mockFilms.filter(
      (f) =>
        f.title.toLowerCase().includes(search) &&
        (!genre || f.genres.includes(genre)) &&
        (f.runtimeMinutes ?? 0) <= limit &&
        (f.voteAverage ?? 0) >= Number(q.get('minVoteAverage') ?? 0) &&
        !seen.has(f.tmdbId),
    )
    if (sort === 'newest') matches.sort((a, b) => (b.releaseYear ?? 0) - (a.releaseYear ?? 0))
    if (sort === 'rating') matches.sort((a, b) => (b.voteAverage ?? 0) - (a.voteAverage ?? 0))
    const items = matches
      .slice((page - 1) * pageSize, page * pageSize)
      .map(({ tmdbId, title, releaseYear, posterPath, runtimeMinutes, genres, voteAverage }) => ({
        tmdbId, title, releaseYear, posterPath, runtimeMinutes, genres, voteAverage,
      }))
    return HttpResponse.json<ResponseOf<'/api/films', 'get', 200>>({ items, page, pageSize, totalCount: matches.length })
  }),
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
  http.get(apiPath('/api/library'), ({ request }) => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const q = new URL(request.url).searchParams
    const shelf = (q.get('shelf') ?? 'liked') as 'liked' | 'watchlist' | 'seen'
    const sort = q.get('sort') ?? 'recent'
    const genre = q.get('genre')
    const page = Number(q.get('page') ?? 1)
    const pageSize = Number(q.get('pageSize') ?? 20)
    if (!['liked', 'watchlist', 'seen'].includes(shelf) || !['recent', 'title', 'rating'].includes(sort) || page < 1 || pageSize < 1 || pageSize > 100) {
      return problem(400, 'validation', { query: ['Check shelf, sort, page and pageSize.'] })
    }
    const onShelf = { liked: (i: { liked: boolean }) => i.liked, watchlist: (i: { onWatchlist: boolean }) => i.onWatchlist, seen: (i: { seen: boolean }) => i.seen }[shelf]
    const items = listMockUserFilms()
      .filter((i) => onShelf(i) && (!genre || i.film.genres.includes(genre)))
    if (sort === 'title') items.sort((a, b) => a.film.title.localeCompare(b.film.title))
    if (sort === 'rating') items.sort((a, b) => (b.rating ?? 0) - (a.rating ?? 0))
    return HttpResponse.json<ResponseOf<'/api/library', 'get', 200>>({
      items: items.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      totalCount: items.length,
    })
  }),
  http.get(apiPath('/api/library/summary'), () => {
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const all = listMockUserFilms()
    return HttpResponse.json<ResponseOf<'/api/library/summary', 'get', 200>>({
      liked: all.filter((i) => i.liked).length,
      watchlist: all.filter((i) => i.onWatchlist).length,
      seen: all.filter((i) => i.seen).length,
    })
  }),
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

const roomHandlers = [
  http.post(apiPath('/api/rooms'), async ({ request }) => {
    const session = getMockSession()
    if (session?.kind !== 'user') return problem(401, 'auth.unauthenticated')
    const body = (await request.json()) as RequestOf<'/api/rooms', 'post'>
    const name = body.name.trim()
    if (name.length < 1 || name.length > 40) return problem(400, 'validation', { name: ['Use 1 to 40 characters.'] })
    const room = createMockRoom(name, body.mode, session)
    return HttpResponse.json<ResponseOf<'/api/rooms', 'post', 201>>(room, {
      status: 201,
      headers: { Location: `/api/rooms/${room.code}` },
    })
  }),
  http.get(apiPath('/api/rooms/{code}/preview'), ({ params }) => {
    const room = findMockRoom(String(params.code))
    if (!room) return problem(404, 'room.not_found')
    const host = room.members.find((m) => m.isHost)
    return HttpResponse.json<ResponseOf<'/api/rooms/{code}/preview', 'get', 200>>({
      code: room.code,
      name: room.name,
      hostDisplayName: host?.displayName ?? '',
      mode: room.mode,
      status: room.status,
      memberCount: room.members.length,
      maxMembers: room.maxMembers,
    })
  }),
  http.post(apiPath('/api/rooms/{code}/join'), async ({ request, params }) => {
    const room = findMockRoom(String(params.code))
    if (!room) return problem(404, 'room.not_found')
    const body = (await request.json()) as RequestOf<'/api/rooms/{code}/join', 'post'>
    const session = getMockSession()
    const existing = mockRoomFor(room.code)
    if (existing) return HttpResponse.json<ResponseOf<'/api/rooms/{code}/join', 'post', 200>>(existing)
    if (room.status === 'closed') return problem(409, 'room.closed')
    if (room.members.length >= MOCK_MAX_MEMBERS) return problem(409, 'room.full')
    const name = body.displayName?.trim() ?? ''
    if (!session && (name.length < 1 || name.length > 30)) {
      return problem(400, 'validation', { displayName: ['Use 1 to 30 characters.'] })
    }
    // A signed-in user joins under their own name and id; an anonymous visitor becomes a guest (cookie → session).
    const isUser = session?.kind === 'user'
    const member = addMockRoomMember(room.code, isUser ? session.displayName : name, {
      kind: isUser ? 'user' : 'guest',
      ...(isUser ? { id: session.id } : {}),
    })
    if (member && !isUser) {
      setMockSession({ kind: 'guest', id: member.id, displayName: member.displayName, email: null, roomCode: room.code })
    }
    const joined = mockRoomFor(room.code)
    return joined ? HttpResponse.json<ResponseOf<'/api/rooms/{code}/join', 'post', 200>>(joined) : problem(500, 'server.error')
  }),
  http.get(apiPath('/api/rooms/{code}'), ({ params }) => {
    if (!findMockRoom(String(params.code))) return problem(404, 'room.not_found')
    if (!getMockSession()) return problem(401, 'auth.unauthenticated')
    const room = mockRoomFor(String(params.code))
    return room ? HttpResponse.json<ResponseOf<'/api/rooms/{code}', 'get', 200>>(room) : problem(403, 'room.not_member')
  }),
  http.post(apiPath('/api/rooms/{code}/start'), ({ params }) => {
    if (!findMockRoom(String(params.code))) return problem(404, 'room.not_found')
    const room = mockRoomFor(String(params.code))
    if (!room) return problem(403, 'room.not_member')
    if (!room.me.isHost) return problem(403, 'room.not_host')
    if (room.status !== 'lobby') return problem(409, 'room.invalid_state')
    setMockRoomStatus(room.code, 'swiping')
    const started = mockRoomFor(room.code)
    return started ? HttpResponse.json<ResponseOf<'/api/rooms/{code}/start', 'post', 200>>(started) : problem(500, 'server.error')
  }),
  http.post(apiPath('/api/rooms/{code}/leave'), ({ params }) => {
    if (!findMockRoom(String(params.code))) return problem(404, 'room.not_found')
    const room = mockRoomFor(String(params.code))
    if (!room) return problem(403, 'room.not_member')
    removeMockRoomMember(room.code, room.me.id)
    return new HttpResponse(null, { status: 204 })
  }),
]

export const handlers = [...healthHandlers, ...authHandlers, ...filmHandlers, ...libraryHandlers, ...watchlistHandlers, ...deckHandlers, ...roomHandlers]
