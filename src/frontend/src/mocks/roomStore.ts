import type { components } from '@/shared/api'
import { getMockSession } from './authStore'

type RoomDto = components['schemas']['RoomDto']
type RoomMemberDto = components['schemas']['RoomMemberDto']
type RoomMode = components['schemas']['RoomMode']
type RoomStatus = components['schemas']['RoomStatus']

export const MOCK_MAX_MEMBERS = 8
/** A lobby the dev server and tests can join without creating one first (hosted by a mock friend). */
export const MOCK_ROOM_CODE = 'POP-428'

type StoredRoom = Omit<RoomDto, 'me'>
const KEY = 'ithaca.mock.rooms'
const CODE_WORDS = ['POP', 'REEL', 'CUT', 'FILM', 'SET', 'LOT']

let counter = 0
let rooms = load()

function seedRoom(): StoredRoom {
  const host = mockMember('Omar', { isHost: true, isReady: true, id: '0190c4a2-0000-7000-8000-0000000000a1' })
  const friend = mockMember('Noor', { isReady: false, id: '0190c4a2-0000-7000-8000-0000000000a2' })
  return {
    id: '0190c4a2-0000-7000-8000-0000000000b1',
    code: MOCK_ROOM_CODE,
    name: 'Friday Film Club',
    mode: 'mood',
    status: 'lobby',
    startsAt: null,
    createdAt: '2026-01-01T18:00:00Z',
    deckSize: 0,
    maxMembers: MOCK_MAX_MEMBERS,
    members: [host, friend],
  }
}

function load(): Map<string, StoredRoom> {
  try {
    const raw = globalThis.sessionStorage?.getItem(KEY)
    if (raw) return new Map(JSON.parse(raw) as [string, StoredRoom][])
  } catch {
    // fall through to the seed
  }
  return new Map([[MOCK_ROOM_CODE, seedRoom()]])
}

function persist() {
  try {
    globalThis.sessionStorage?.setItem(KEY, JSON.stringify([...rooms]))
  } catch {
    // No storage: in-memory is enough.
  }
}

function mockMember(
  displayName: string,
  over: Partial<RoomMemberDto> = {},
): RoomMemberDto {
  return {
    id: `0190c4a2-0000-7000-8000-${String(900 + counter++).padStart(12, '0')}`,
    displayName,
    kind: 'user',
    isHost: false,
    isReady: false,
    joinedAt: '2026-01-01T18:00:00Z',
    ...over,
  }
}

const norm = (code: string) => decodeURIComponent(code).trim().toUpperCase()

export function findMockRoom(code: string): StoredRoom | undefined {
  return rooms.get(norm(code))
}

/** The room as the current session sees it, or `undefined` when the caller isn't a member. */
export function mockRoomFor(code: string): RoomDto | undefined {
  const room = findMockRoom(code)
  const me = room?.members.find((m) => m.id === getMockSession()?.id)
  return room && me ? { ...room, me } : undefined
}

/** In a justSwipe room everyone is ready; in a mood room a member is ready after their mood answer (FE-10). */
const readyFor = (mode: RoomMode) => mode === 'justSwipe'

export function createMockRoom(name: string, mode: RoomMode, host: { id: string; displayName: string }): RoomDto {
  const letters = CODE_WORDS[rooms.size % CODE_WORDS.length]?.slice(0, 3) ?? 'POP'
  let code = ''
  do code = `${letters}-${String(100 + ((rooms.size * 137 + counter++ * 53) % 900))}`
  while (rooms.has(code))
  const member: RoomMemberDto = mockMember(host.displayName, { id: host.id, isHost: true, isReady: readyFor(mode) })
  const room: StoredRoom = {
    id: `0190c4a2-0000-7000-8000-${String(5000 + rooms.size).padStart(12, '0')}`,
    code,
    name,
    mode,
    status: 'lobby',
    startsAt: null,
    createdAt: new Date().toISOString(),
    deckSize: mode === 'justSwipe' ? 12 : 0,
    maxMembers: MOCK_MAX_MEMBERS,
    members: [member],
  }
  rooms.set(code, room)
  persist()
  return { ...room, me: member }
}

/** Adds a member (a guest or a user). Scriptable from tests: `addMockRoomMember(code, 'Lina')`. */
export function addMockRoomMember(
  code: string,
  displayName: string,
  over: Partial<RoomMemberDto> = {},
): RoomMemberDto | undefined {
  const room = findMockRoom(code)
  if (!room) return undefined
  const member = mockMember(displayName, { isReady: readyFor(room.mode), ...over })
  room.members.push(member)
  persist()
  return member
}

export function removeMockRoomMember(code: string, memberId: string) {
  const room = findMockRoom(code)
  if (!room) return
  room.members = room.members.filter((m) => m.id !== memberId)
  persist()
}

export function setMockRoomStatus(code: string, status: RoomStatus) {
  const room = findMockRoom(code)
  if (room) {
    room.status = status
    persist()
  }
}

export function setMockMemberReady(code: string, memberId: string, isReady: boolean) {
  const member = findMockRoom(code)?.members.find((m) => m.id === memberId)
  if (member) {
    member.isReady = isReady
    persist()
  }
}

/** Called from src/test/setup.ts after every test. */
export function resetMockRooms() {
  counter = 0
  rooms = new Map([[MOCK_ROOM_CODE, seedRoom()]])
  try {
    globalThis.sessionStorage?.removeItem(KEY)
  } catch {
    // ignore
  }
}
