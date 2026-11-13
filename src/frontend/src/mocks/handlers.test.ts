import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { ApiError, api } from '@/shared/api'
import { apiPath } from './http'
import { server } from './server'

async function rejection(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    if (error instanceof ApiError) return error
    throw error
  }
  throw new Error('expected the call to fail')
}

describe('mock API', () => {
  it('answers /api/health', async () => {
    const { data } = await api.GET('/api/health')
    expect(data).toEqual({ status: 'ok' })
  })

  it('serves a seeded film', async () => {
    const { data } = await api.GET('/api/films/{tmdbId}', { params: { path: { tmdbId: 550 } } })
    expect(data?.title).toBe('Fight Club')
  })

  it('returns the contract error for an unknown film', async () => {
    const error = await rejection(api.GET('/api/films/{tmdbId}', { params: { path: { tmdbId: 1 } } }))
    expect(error).toMatchObject({ status: 404, code: 'film.not_found' })
  })

  it('lets a test override a handler for one case', async () => {
    server.use(http.get(apiPath('/api/health'), () => HttpResponse.json({ status: 'degraded' })))
    const { data } = await api.GET('/api/health')
    expect(data?.status).toBe('degraded')
  })

  it.each([3.25, 0, 5.5, 7])('rejects the rating %s with a 400 on `rating` (half steps 0.5 to 5 only)', async (rating) => {
    const error = await rejection(
      api.PUT('/api/library/{tmdbId}', { params: { path: { tmdbId: 550 } }, body: { liked: false, onWatchlist: false, seen: true, rating } }),
    )
    expect(error).toMatchObject({ status: 400, code: 'validation', fieldErrors: { rating: expect.any(Array) as string[] } })
  })

  it.each([0.5, 3.5, 5])('accepts the rating %s', async (rating) => {
    const { data } = await api.PUT('/api/library/{tmdbId}', {
      params: { path: { tmdbId: 550 } },
      body: { liked: false, onWatchlist: false, seen: true, rating },
    })
    expect(data?.rating).toBe(rating)
  })
})
