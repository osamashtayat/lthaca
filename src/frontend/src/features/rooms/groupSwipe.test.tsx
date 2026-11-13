// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MOCK_USER } from '@/mocks/authStore'
import { failRoomVote } from '@/mocks/overrides'
import {
  addMockRoomMember,
  castMockVote,
  createMockRoom,
  findMockRoom,
  matchMockRoom,
  MOCK_ROOM_CODE,
  scriptMockVotes,
  setMockRoomStatus,
} from '@/mocks/roomStore'
import { server } from '@/mocks/server'
import { queryKeys } from '@/shared/query'
import { RoomResultPage, RoomSwipePage } from '@/features/rooms'

vi.mock('@/shared/notify', () => ({
  notify: { apiError: vi.fn<(e?: unknown) => void>(), error: vi.fn<(m: string) => void>(), success: vi.fn<(m: string) => void>(), info: vi.fn<(m: string) => void>() },
}))

afterEach(cleanup)

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>
}

function renderAt(path: string, client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })) {
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/rooms/:code/swipe" element={<RoomSwipePage />} />
          <Route path="/rooms/:code/result" element={<RoomResultPage />} />
          <Route path="/rooms/:code" element={<p>lobby screen</p>} />
          <Route path="/" element={<p>Tonight</p>} />
        </Routes>
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return client
}

/** A started justSwipe room hosted by the signed-in mock user (Lina) with one friend, Omar. */
function startedRoom() {
  const room = createMockRoom('Friday Film Club', 'justSwipe', MOCK_USER)
  const omar = addMockRoomMember(room.code, 'Omar')
  setMockRoomStatus(room.code, 'swiping')
  return { code: room.code, omarId: omar?.id ?? '', memberId: room.me.id }
}

describe('Group swipe', () => {
  it('votes through the deck, counting "N OF M" and who is still swiping', async () => {
    const { code } = startedRoom()
    const user = userEvent.setup()
    renderAt(`/rooms/${code}/swipe`)
    expect(await screen.findByText(/LIVE · 2 SWIPING · 0 OF 12/)).toBeTruthy()
    await user.click(screen.getByRole('button', { name: "I'd watch it" }))
    expect(await screen.findByText(/1 OF 12/)).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Not for me' }))
    expect(await screen.findByText(/2 OF 12/)).toBeTruthy()
    expect(Object.keys(findMockRoom(code)?.votes ?? {})).toHaveLength(2)
  })

  it('puts the card back and says so when a vote fails', async () => {
    const { code } = startedRoom()
    server.use(failRoomVote())
    const user = userEvent.setup()
    renderAt(`/rooms/${code}/swipe`)
    await user.click(await screen.findByRole('button', { name: "I'd watch it" }))
    await waitFor(() => expect(screen.getByText(/0 OF 12/)).toBeTruthy())
    expect(screen.getByText('Fight Club')).toBeTruthy()
  })

  it('counts a repeated voteId once (idempotent) and refuses a different vote on a voted card', () => {
    const { code, memberId } = startedRoom()
    const itemId = Object.keys(findMockRoom(code)?.votes ?? {})[0] ?? `${findMockRoom(code)?.id.slice(0, 24)}${String(1).padStart(12, '0')}`
    const vote = { voteId: 'v1', deckItemId: itemId, direction: 'right' as const }
    expect(castMockVote(code, memberId, vote)).toBe('ok')
    expect(castMockVote(code, memberId, vote)).toBe('ok')
    expect(castMockVote(code, memberId, { ...vote, voteId: 'v2' })).toBe('already_voted')
    expect(Object.keys(findMockRoom(code)?.votes[itemId] ?? {})).toHaveLength(1)
  })

  it('moves everyone to the result when the room matches', async () => {
    const { code } = startedRoom()
    const client = renderAt(`/rooms/${code}/swipe`)
    await screen.findByText(/LIVE/)
    matchMockRoom(code, 27205) // the server decided; roomMatched makes the client re-read the room
    await client.invalidateQueries({ queryKey: queryKeys.rooms.detail(code), exact: true })
    expect(await screen.findByRole('heading', { name: "It's a match!" })).toBeTruthy()
    expect(screen.getByTestId('where').textContent).toBe(`/rooms/${code}/result`)
    expect(screen.getByText('Inception')).toBeTruthy()
  })

  it('shows "Finish now" to the host only', async () => {
    startedRoom()
    renderAt(`/rooms/${startedRoom().code}/swipe`)
    expect(await screen.findByRole('button', { name: 'Finish now' })).toBeTruthy()
    cleanup()
    addMockRoomMember(MOCK_ROOM_CODE, 'Lina', { id: MOCK_USER.id })
    setMockRoomStatus(MOCK_ROOM_CODE, 'swiping')
    renderAt(`/rooms/${MOCK_ROOM_CODE}/swipe`)
    await screen.findByText(/LIVE/)
    expect(screen.queryByRole('button', { name: 'Finish now' })).toBeNull()
  })

  it('goes back to the lobby while the room has not started, and ends in a closed room', async () => {
    const room = createMockRoom('Club', 'justSwipe', MOCK_USER)
    renderAt(`/rooms/${room.code}/swipe`)
    expect(await screen.findByText('lobby screen')).toBeTruthy()
    cleanup()
    setMockRoomStatus(room.code, 'closed')
    renderAt(`/rooms/${room.code}/swipe`)
    expect((await screen.findByRole('alert')).textContent).toMatch(/closed/)
  })
})

describe('Result', () => {
  it('host finish: a close call that names who said yes or has seen it and only counts the noes', async () => {
    const { code, omarId, memberId } = startedRoom()
    const noor = addMockRoomMember(code, 'Noor')?.id ?? ''
    scriptMockVotes(code, memberId, { 1: 'right', 2: 'right' })
    scriptMockVotes(code, omarId, { 1: 'right', 2: 'up' })
    scriptMockVotes(code, noor, { 1: 'left', 2: 'left' })
    const user = userEvent.setup()
    renderAt(`/rooms/${code}/swipe`)
    await user.click(await screen.findByRole('button', { name: 'Finish now' }))
    expect(await screen.findByRole('heading', { name: 'Close call' })).toBeTruthy()
    expect(screen.getByText('Lina and Omar want to watch it')).toBeTruthy()
    expect(screen.getByText("Omar passed: they've seen it")).toBeTruthy()
    expect(screen.getAllByText('1 said no').length).toBeGreaterThan(0)
    expect(screen.queryByText(/Noor said/)).toBeNull() // who said no is never shown
    expect(screen.getByTestId('where').textContent).toBe(`/rooms/${code}/result`)
  })

  it('a match shows one film in the big-moment heading', async () => {
    const { code } = startedRoom()
    matchMockRoom(code, 550)
    renderAt(`/rooms/${code}/result`)
    expect(await screen.findByRole('heading', { name: "It's a match!" })).toBeTruthy()
    expect(screen.getAllByRole('listitem').length).toBeGreaterThan(0)
    expect(screen.getByText('Fight Club')).toBeTruthy()
  })

  it('sends a room that is still swiping back to the deck', async () => {
    const { code } = startedRoom()
    renderAt(`/rooms/${code}/result`)
    await waitFor(() => expect(screen.getByTestId('where').textContent).toBe(`/rooms/${code}/swipe`))
  })
})
