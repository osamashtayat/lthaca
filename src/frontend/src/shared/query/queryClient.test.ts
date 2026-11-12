import { describe, expect, it } from 'vitest'
import { ApiError } from '@/shared/api/apiError'
import { shouldRetryQuery } from './queryClient'

describe('shouldRetryQuery', () => {
  it('never retries a 4xx', () => {
    expect(shouldRetryQuery(0, new ApiError({ status: 404, code: 'not_found' }))).toBe(false)
  })

  it('retries a transient failure once', () => {
    expect(shouldRetryQuery(0, new ApiError({ status: 503, code: 'server.error' }))).toBe(true)
    expect(shouldRetryQuery(1, new ApiError({ status: 503, code: 'server.error' }))).toBe(false)
  })
})
