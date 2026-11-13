import type { SwipeDirection } from './types'

export type Offset = { x: number; y: number }

/** How far (px) a drag must travel before releasing it counts as a swipe. */
export const SWIPE_THRESHOLD = 80

const ALL: readonly SwipeDirection[] = ['right', 'left', 'up']

/**
 * Which swipe a drag amounts to, or `null` (not far enough, or downwards, which is never a swipe). The dominant axis
 * wins, so a mostly-sideways drag that also drifts up is still left/right. `allowed` lets a screen without "seen it"
 * (the group deck) ignore up.
 */
export function directionFromDrag(
  { x, y }: Offset,
  threshold: number = SWIPE_THRESHOLD,
  allowed: readonly SwipeDirection[] = ALL,
): SwipeDirection | null {
  const ax = Math.abs(x)
  const ay = Math.abs(y)
  let direction: SwipeDirection | null = null
  if (y < 0 && ay >= threshold && ay > ax) direction = 'up'
  else if (ax >= threshold && ax >= ay) direction = x > 0 ? 'right' : 'left'
  return direction !== null && allowed.includes(direction) ? direction : null
}
