// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import type { RoomEventHandlers } from '@/shared/realtime'
import { queryKeys } from '@/shared/query'

const captured: { handlers: RoomEventHandlers; onSynced: () => void } = { handlers: {}, onSynced: () => undefined }
vi.mock('@/shared/realtime', () => ({
  useRoomHub: ({ handlers, onSynced }: { handlers: RoomEventHandlers; onSynced: () => void }) => {
    captured.handlers = handlers
    captured.onSynced = onSynced
    return { status: 'connected' }
  },
}))

import { useRoomLive } from './useRoomLive'

describe('useRoomLive', () => {
  it.each(['memberJoined', 'memberLeft', 'memberReady', 'roomStarted', 'roomMatched', 'roomClosed'] as const)(
    'refetches the room itself on %s',
    (event) => {
      const client = new QueryClient()
      const spy = vi.spyOn(client, 'invalidateQueries')
      const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>
      renderHook(() => useRoomLive('pop-428', true), { wrapper })
      captured.handlers[event]?.({})
      expect(spy).toHaveBeenCalledWith({ queryKey: queryKeys.rooms.detail('POP-428'), exact: true })
    },
  )

  it.each([
    ['voteCast', queryKeys.rooms.progress('POP-428')],
    ['deckExtended', queryKeys.rooms.deck('POP-428')],
  ] as const)('refetches only the matching part on %s', (event, queryKey) => {
    const client = new QueryClient()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>
    renderHook(() => useRoomLive('pop-428', true), { wrapper })
    captured.handlers[event]?.({})
    expect(spy).toHaveBeenCalledWith({ queryKey })
  })

  it('refetches every part of the room after a reconnect (events may have been missed)', () => {
    const client = new QueryClient()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>
    renderHook(() => useRoomLive('pop-428', true), { wrapper })
    captured.onSynced()
    expect(spy).toHaveBeenCalledWith({ queryKey: queryKeys.rooms.detail('POP-428') }) // no `exact`: deck, progress and result too
  })
})
