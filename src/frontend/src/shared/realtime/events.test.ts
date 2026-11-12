import { describe, expect, it } from 'vitest'
import { ROOM_EVENTS, ROOM_HUB_PATH } from './events'

describe('room hub contract', () => {
  it('reads the hub path and events from contracts/realtime-events.json', () => {
    expect(ROOM_HUB_PATH).toBe('/hubs/rooms')
    expect(ROOM_EVENTS).toContain('voteCast')
  })
})
