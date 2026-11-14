import { describe, expect, it } from 'vitest'
import { formatDate, formatSince, initialOf } from './format'

describe('profile formatting', () => {
  it('formats the join month in UTC', () => {
    expect(formatSince('2026-11-01T00:30:00Z')).toBe('Nov 2026')
  })
  it('formats a day', () => {
    expect(formatDate('2026-11-13T12:00:00Z')).toBe('Nov 13, 2026')
  })
  it('takes an upper-case initial, or ? for a blank name', () => {
    expect(initialOf(' lina')).toBe('L')
    expect(initialOf('  ')).toBe('?')
  })
})
