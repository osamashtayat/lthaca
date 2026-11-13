import { describe, expect, it } from 'vitest'
import { directionFromDrag } from './swipeGesture'

describe('directionFromDrag', () => {
  it.each([
    [{ x: 120, y: 10 }, 'right'],
    [{ x: -120, y: -30 }, 'left'],
    [{ x: 10, y: -120 }, 'up'],
    [{ x: 100, y: -90 }, 'right'], // sideways dominates a diagonal
    [{ x: 40, y: -110 }, 'up'],
  ] as const)('%j → %s', (offset, expected) => {
    expect(directionFromDrag(offset)).toBe(expected)
  })

  it.each([
    [{ x: 30, y: -20 }],
    [{ x: 0, y: 0 }],
    [{ x: 10, y: 200 }], // down is never a swipe
    [{ x: 79, y: -79 }],
  ])('%j → no swipe', (offset) => {
    expect(directionFromDrag(offset)).toBeNull()
  })

  it('ignores directions a screen does not allow', () => {
    expect(directionFromDrag({ x: 0, y: -200 }, 80, ['left', 'right'])).toBeNull()
    expect(directionFromDrag({ x: 200, y: 0 }, 80, ['left', 'right'])).toBe('right')
  })
})
