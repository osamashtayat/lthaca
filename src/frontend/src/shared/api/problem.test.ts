import { describe, expect, it } from 'vitest'
import { ApiError, NETWORK_ERROR_CODE } from './apiError'
import { PROBLEM_MESSAGES, toUserMessage } from './problem'

describe('toUserMessage', () => {
  it('maps a known code', () => {
    expect(toUserMessage(new ApiError({ status: 404, code: 'film.not_found' }))).toBe(PROBLEM_MESSAGES['film.not_found'])
  })

  it('explains a network failure', () => {
    expect(toUserMessage(new ApiError({ status: 0, code: NETWORK_ERROR_CODE }))).toMatch(/connection/)
  })

  it('falls back by status, never showing server text', () => {
    expect(toUserMessage(new ApiError({ status: 503, code: 'unknown' }))).toBe(PROBLEM_MESSAGES['server.error'])
    expect(toUserMessage(new Error('boom'))).not.toContain('boom')
  })
})
