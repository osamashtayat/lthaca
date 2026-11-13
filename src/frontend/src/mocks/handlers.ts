import { http, HttpResponse } from 'msw'
import { addMockAccount, findMockAccount, getMockSession, isMockEmailTaken, setMockSession } from './authStore'
import { findMockFilm } from './fixtures'
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

export const handlers = [...healthHandlers, ...authHandlers, ...filmHandlers]
