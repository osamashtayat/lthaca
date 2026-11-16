import { getClient } from '@sentry/react'
import { describe, expect, it } from 'vitest'
import { ERROR_TRACKING_ENABLED, initErrorTracking, nameRoute, scrubBreadcrumb, scrubEvent, scrubSpan, withoutQueries } from './errorTracking'

describe('error tracking (ADR-0028)', () => {
  it('is off in tests, so nothing is ever sent', () => {
    initErrorTracking()
    expect(ERROR_TRACKING_ENABLED).toBe(false)
    expect(getClient()).toBeUndefined()
  })

  it('strips query strings from absolute and root-relative URLs only', () => {
    expect(withoutQueries('https://ithaca.test/reset-password?token=abc#x')).toBe('https://ithaca.test/reset-password')
    expect(withoutQueries('GET /api/films?search=secret crush')).toBe('GET /api/films crush')
    expect(withoutQueries('nothing to see?really')).toBe('nothing to see?really')
  })

  it('keeps only the URL without its query, the User-Agent and the user id on an event', () => {
    const event = scrubEvent({
      request: {
        url: 'https://ithaca.test/browse?search=my+secret',
        query_string: 'search=my+secret',
        cookies: { session: 'x' },
        headers: { 'User-Agent': 'Mobile Safari', Referer: 'https://ithaca.test/reset-password?token=abc' },
        data: { email: 'ada@example.com' },
      },
      user: { id: '42', email: 'ada@example.com', ip_address: '203.0.113.9' },
    })
    expect(event.request).toEqual({ url: 'https://ithaca.test/browse', headers: { 'User-Agent': 'Mobile Safari' } })
    expect(event.user).toEqual({ id: '42', ip_address: null })
  })

  it('names traces by route pattern, never by the room code, id or query in the URL', () => {
    const routes = [
      {
        element: null,
        children: [
          { index: true, element: null },
          { path: '/rooms/:code/swipe', element: null },
          { element: null, children: [{ path: '/deck/:deckId', element: null }] },
          { path: '*', element: null },
        ],
      },
    ]
    expect(nameRoute('/rooms/ABC123/swipe', routes)).toBe('/rooms/:code/swipe')
    expect(nameRoute('/deck/0190a1b2-c3d4', routes)).toBe('/deck/:deckId')
    expect(nameRoute('/', routes)).toBe('/')
    expect(nameRoute('/nowhere/at/all', routes)).toBe('/*')
  })

  it('removes queries from span names and attributes', () => {
    const span = scrubSpan({
      name: 'GET /api/films?search=secret',
      attributes: { 'url.full': { value: 'https://ithaca.test/api/films?search=secret', type: 'string' }, 'http.response.status_code': 200 },
    })
    expect(span.name).toBe('GET /api/films')
    expect(span.attributes).toEqual({ 'url.full': { value: 'https://ithaca.test/api/films', type: 'string' }, 'http.response.status_code': 200 })
  })

  it('removes queries from navigation and fetch breadcrumbs', () => {
    const breadcrumb = scrubBreadcrumb({
      message: 'navigated',
      data: { from: '/reset-password?token=abc', to: '/sign-in', url: '/api/films?search=x', nested: ['/a?b=c'] },
    })
    expect(breadcrumb.data).toEqual({ from: '/reset-password', to: '/sign-in', url: '/api/films', nested: ['/a'] })
  })
})
