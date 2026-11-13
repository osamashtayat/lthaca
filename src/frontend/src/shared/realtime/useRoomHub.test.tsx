// @vitest-environment jsdom
import { renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

const invoke = vi.fn<(method: string, ...args: unknown[]) => Promise<void>>().mockResolvedValue(undefined)
const reconnected: { current: () => void } = { current: () => undefined }

vi.mock('@/shared/api/mode', () => ({ REALTIME_ENABLED: true, API_MOCKS_ENABLED: false }))
vi.mock('./roomHub', () => ({
  HUB_METHODS: { joinRoom: 'JoinRoom', leaveRoom: 'LeaveRoom' },
  statusOf: () => 'connected',
  createRoomHubConnection: () => ({
    on: vi.fn<() => void>(),
    onreconnecting: vi.fn<() => void>(),
    onreconnected: (cb: () => void) => {
      reconnected.current = cb
    },
    onclose: vi.fn<() => void>(),
    start: () => Promise.resolve(),
    stop: () => Promise.resolve(),
    invoke,
  }),
}))

import { useRoomHub } from './useRoomHub'

describe('useRoomHub with a room', () => {
  it('joins the room group once connected and again after a reconnect, then asks the screen to refetch', async () => {
    const onSynced = vi.fn<() => void>()
    renderHook(() => useRoomHub({ room: 'POP-428', onSynced }))
    await vi.waitFor(() => expect(onSynced).toHaveBeenCalledTimes(1))
    expect(invoke).toHaveBeenCalledWith('JoinRoom', 'POP-428')
    reconnected.current()
    await vi.waitFor(() => expect(onSynced).toHaveBeenCalledTimes(2))
    expect(invoke).toHaveBeenCalledTimes(2)
  })
})
