import { describe, expect, it } from 'vitest'
import { safeNextPath } from './nextPath'

describe('safeNextPath', () => {
  it('keeps in-app paths with their query', () => {
    expect(safeNextPath('/cinema?shelf=liked')).toBe('/cinema?shelf=liked')
  })
  it.each([null, undefined, '', 'cinema', '//evil.example', 'https://evil.example', '/\\evil.example'])(
    'falls back to / for %s',
    (value) => {
      expect(safeNextPath(value)).toBe('/')
    },
  )
})
