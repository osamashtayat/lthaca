import { useRef, useState } from 'react'
import type { PointerEvent } from 'react'
import { directionFromDrag, type Offset } from './swipeGesture'
import type { SwipeDirection } from './types'

const NO_OFFSET: Offset = { x: 0, y: 0 }

type Options = {
  /** Called when a drag is released far enough in an allowed direction. */
  onSwipe: (direction: SwipeDirection) => void
  disabled?: boolean
  allowed?: readonly SwipeDirection[]
}

/**
 * Drag-to-swipe for one card with pointer events (touch, mouse and pen alike). Spread `handlers` on the card (which
 * needs `touch-action: none`) and move it by `offset` while `dragging`. A release that doesn't amount to a swipe
 * springs back. The direction maths is the pure `directionFromDrag`. Reused by the group deck (FE-11).
 */
export function useSwipeGesture({ onSwipe, disabled = false, allowed }: Options) {
  const origin = useRef<{ x: number; y: number } | null>(null)
  const latest = useRef<Offset>(NO_OFFSET)
  const [offset, setOffset] = useState<Offset>(NO_OFFSET)
  const [dragging, setDragging] = useState(false)

  function finish(commit: boolean) {
    if (!origin.current) return
    const direction = commit ? directionFromDrag(latest.current, undefined, allowed) : null
    origin.current = null
    latest.current = NO_OFFSET
    setDragging(false)
    setOffset(NO_OFFSET)
    if (direction) onSwipe(direction)
  }

  const handlers = {
    onPointerDown(e: PointerEvent<HTMLElement>) {
      if (disabled || (e.pointerType === 'mouse' && e.button !== 0)) return
      e.currentTarget.setPointerCapture?.(e.pointerId)
      origin.current = { x: e.clientX, y: e.clientY }
      setDragging(true)
    },
    onPointerMove(e: PointerEvent<HTMLElement>) {
      if (!origin.current) return
      latest.current = { x: e.clientX - origin.current.x, y: e.clientY - origin.current.y }
      setOffset(latest.current)
    },
    onPointerUp: () => finish(true),
    onPointerCancel: () => finish(false),
  }

  return {
    handlers,
    offset,
    dragging,
    /** The swipe a release would make right now (for the card's hint labels), or null. */
    pending: dragging ? directionFromDrag(offset, 40, allowed) : null,
  }
}
