import contract from '@contracts/realtime-events.json'

/**
 * Room hub path and event names, read from contracts/realtime-events.json — the same file the backend's
 * RoomEvents.cs is tested against. Never type an event name as a string literal elsewhere.
 */
export const ROOM_HUB_PATH: string = contract.hubPath

export type RoomEvent = keyof typeof contract.serverToClient

export const ROOM_EVENTS = Object.keys(contract.serverToClient) as RoomEvent[]
