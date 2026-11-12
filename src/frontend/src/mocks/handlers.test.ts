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
})
