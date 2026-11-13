import type { components } from '@/shared/api'
import { getMockSession } from './authStore'
import { mockFilms } from './fixtures'

type RoomDto = components['schemas']['RoomDto']
type RoomMemberDto = components['schemas']['RoomMemberDto']
type RoomMode = components['schemas']['RoomMode']
type RoomStatus = components['schemas']['RoomStatus']
type DeckItemDto = components['schemas']['DeckItemDto']
type SwipeDirection = components['schemas']['SwipeDirection']
type RoomResultDto = components['schemas']['RoomResultDto']
type RoomPickDto = components['schemas']['RoomPickDto']
type RoomProgressDto = components['schemas']['RoomProgressDto']

/** One member's vote on one deck item. */
type StoredVote = { voteId: string; direction: SwipeDirection }

export const MOCK_MAX_MEMBERS = 8
export const MOCK_DECK_SIZE = 12
/** A lobby the dev server and tests can join without creating one first (hosted by a mock friend). */
export const MOCK_ROOM_CODE = 'POP-428'

type StoredRoom = Omit<RoomDto, 'me'> & {
  /** itemId → memberId → vote. */
  votes: Record<string, Record<string, StoredVote>>
  result: RoomResultDto | null
}
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
    votes: {},
    result: null,
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
  if (!room || !me) return undefined
  const { votes: _votes, result: _result, ...dto } = room
  return { ...dto, me }
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
    deckSize: mode === 'justSwipe' ? MOCK_DECK_SIZE : 0,
    maxMembers: MOCK_MAX_MEMBERS,
    members: [member],
    votes: {},
    result: null,
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
    // The mock deck is the first films of the catalog; it exists once swiping starts.
    if (status === 'swiping' && room.deckSize === 0) room.deckSize = Math.min(MOCK_DECK_SIZE, mockFilms.length)
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

const itemId = (room: StoredRoom, position: number) => `${room.id.slice(0, 24)}${String(position).padStart(12, '0')}`

/** The room deck as the current member sees it: `swipe` is their own vote. */
export function mockRoomDeck(code: string, fromPosition = 0): DeckItemDto[] | undefined {
  const room = findMockRoom(code)
  const me = getMockSession()?.id
  if (!room) return undefined
  return Array.from({ length: room.deckSize }, (_, i) => i + 1)
    .filter((position) => position > fromPosition)
    .map((position) => {
      const film = mockFilms[position - 1]
      if (!film) throw new Error(`no mock film at position ${position}`)
      const id = itemId(room, position)
      const mine = me ? room.votes[id]?.[me] : undefined
      return { id, position, film, swipe: mine ? { direction: mine.direction, seenVerdict: null } : null, onWatchlist: false }
    })
}

export type VoteOutcome = 'ok' | 'item_not_found' | 'already_voted'

/** Records a vote; repeating the same `voteId` is a no-op success. A unanimous `right` makes the match (the real rule is the server's). */
export function castMockVote(code: string, memberId: string, vote: { voteId: string; deckItemId: string; direction: SwipeDirection }): VoteOutcome {
  const room = findMockRoom(code)
  if (!room) return 'item_not_found'
  const known = Array.from({ length: room.deckSize }, (_, i) => itemId(room, i + 1))
  if (!known.includes(vote.deckItemId)) return 'item_not_found'
  const byMember = (room.votes[vote.deckItemId] ??= {})
  const existing = byMember[memberId]
  if (existing) return existing.voteId === vote.voteId ? 'ok' : 'already_voted'
  byMember[memberId] = { voteId: vote.voteId, direction: vote.direction }
  const everyoneSaidYes = room.members.every((m) => byMember[m.id]?.direction === 'right')
  if (everyoneSaidYes && !room.result) {
    const film = mockFilms[known.indexOf(vote.deckItemId)]
    if (film) matchMockRoom(code, film.tmdbId)
  }
  persist()
  return 'ok'
}

/** Scripts a friend's votes: `{ [position]: direction }` (1-based). For tests and the dev server. */
export function scriptMockVotes(code: string, memberId: string, byPosition: Record<number, SwipeDirection>) {
  const room = findMockRoom(code)
  if (!room) return
  for (const [position, direction] of Object.entries(byPosition)) {
    castMockVote(code, memberId, { voteId: `script-${memberId}-${position}`, deckItemId: itemId(room, Number(position)), direction })
  }
}

export function mockRoomProgress(code: string): RoomProgressDto | undefined {
  const room = findMockRoom(code)
  if (!room) return undefined
  return {
    status: room.status,
    deckSize: room.deckSize,
    members: room.members.map((m) => ({ memberId: m.id, votedCount: Object.values(room.votes).filter((v) => v[m.id]).length })),
  }
}

function pickFor(room: StoredRoom, position: number, rank: number): RoomPickDto | undefined {
  const film = mockFilms[position - 1]
  const votes = room.votes[itemId(room, position)] ?? {}
  if (!film) return undefined
  const ids = (direction: SwipeDirection) => room.members.filter((m) => votes[m.id]?.direction === direction).map((m) => m.id)
  return { rank, film, yesCount: ids('right').length, noCount: ids('left').length, yes: ids('right'), seen: ids('up') }
}

/** Scripts the match (every member said yes). The `roomMatched` event is the test's to fire. */
export function matchMockRoom(code: string, tmdbId: number) {
  const room = findMockRoom(code)
  const position = mockFilms.findIndex((f) => f.tmdbId === tmdbId) + 1
  if (!room || position === 0) return
  const film = mockFilms[position - 1]
  if (!film) return
  const everyone = room.members.map((m) => m.id)
  room.result = {
    kind: 'match',
    picks: [{ rank: 1, film, yesCount: everyone.length, noCount: 0, yes: everyone, seen: [] }],
    memberCount: room.members.length,
    decidedAt: new Date().toISOString(),
  }
  room.status = 'result'
  persist()
}

/** The host's "finish": a compromise of up to three films by yes votes (ties: fewer noes), or a match if one exists already. */
export function finishMockRoom(code: string): RoomResultDto | undefined {
  const room = findMockRoom(code)
  if (!room) return undefined
  if (!room.result) {
    const picks = Array.from({ length: room.deckSize }, (_, i) => pickFor(room, i + 1, 0))
      .filter((p): p is RoomPickDto => p !== undefined && p.yesCount > 0)
      .sort((a, b) => b.yesCount - a.yesCount || a.noCount - b.noCount)
      .slice(0, 3)
      .map((p, i) => ({ ...p, rank: i + 1 }))
    room.result = { kind: 'compromise', picks, memberCount: room.members.length, decidedAt: new Date().toISOString() }
    room.status = 'result'
    persist()
  }
  return room.result
}

export function mockRoomResult(code: string): RoomResultDto | null | undefined {
  return findMockRoom(code)?.result
}
