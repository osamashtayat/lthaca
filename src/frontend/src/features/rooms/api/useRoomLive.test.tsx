// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import type { RoomEventHandlers } from '@/shared/realtime'
import { queryKeys } from '@/shared/query'

const captured: { handlers: RoomEventHandlers } = { handlers: {} }
vi.mock('@/shared/realtime', () => ({
  useRoomHub: ({ handlers }: { handlers: RoomEventHandlers }) => {
    captured.handlers = handlers
    return { status: 'connected' }
  },
}))

import { useRoomLive } from './useRoomLive'

describe('useRoomLive', () => {
  it.each(['memberJoined', 'memberLeft', 'memberReady', 'roomStarted', 'roomMatched', 'roomClosed'] as const)(
    'refetches the room on %s',
    (event) => {
      const client = new QueryClient()
      const spy = vi.spyOn(client, 'invalidateQueries')
      const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>
      renderHook(() => useRoomLive('pop-428', true), { wrapper })
      captured.handlers[event]?.({})
      expect(spy).toHaveBeenCalledWith({ queryKey: queryKeys.rooms.detail('POP-428') })
    },
  )
})
