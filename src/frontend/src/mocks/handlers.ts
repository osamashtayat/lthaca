import { http, HttpResponse } from 'msw'
import { findMockFilm } from './fixtures'
import { apiPath, problem, type ResponseOf } from './http'

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
]

export const handlers = [...healthHandlers, ...filmHandlers]
