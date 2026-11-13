// @vitest-environment jsdom
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it } from 'vitest'
import { server } from '@/mocks/server'
import { api, readXsrfToken } from './client'

function clearCookie() {
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
}

afterEach(clearCookie)

describe('readXsrfToken', () => {
  it('finds the token among other cookies and decodes it', () => {
    expect(readXsrfToken('a=1; XSRF-TOKEN=abc%3D%3D; b=2')).toBe('abc==')
  })
  it('is undefined without the cookie', () => {
    expect(readXsrfToken('a=1')).toBeUndefined()
    expect(readXsrfToken('')).toBeUndefined()
  })
})

describe('antiforgery header', () => {
  it('sends X-XSRF-TOKEN from the cookie on unsafe methods', async () => {
    document.cookie = 'XSRF-TOKEN=token-123; path=/'
    let seen: string | null = 'unset'
    server.use(
      http.post('*/api/auth/sign-out', ({ request }) => {
        seen = request.headers.get('X-XSRF-TOKEN')
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await api.POST('/api/auth/sign-out')
    expect(seen).toBe('token-123')
  })

  it('does not send it on GET', async () => {
    document.cookie = 'XSRF-TOKEN=token-123; path=/'
    let seen: string | null = 'unset'
    server.use(
      http.get('*/api/auth/me', ({ request }) => {
        seen = request.headers.get('X-XSRF-TOKEN')
        return HttpResponse.json({ kind: 'user', id: '1', displayName: 'A', email: null, roomCode: null })
      }),
    )
    await api.GET('/api/auth/me')
    expect(seen).toBeNull()
  })

  it('sends no header when there is no cookie', async () => {
    let seen: string | null = 'unset'
    server.use(
      http.post('*/api/auth/sign-out', ({ request }) => {
        seen = request.headers.get('X-XSRF-TOKEN')
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await api.POST('/api/auth/sign-out')
    expect(seen).toBeNull()
  })
})
