// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { setMockSession } from '@/mocks/authStore'
import { server } from '@/mocks/server'
import { notHostStart } from '@/mocks/overrides'
import { addMockRoomMember, MOCK_ROOM_CODE, mockRoomFor, setMockMemberReady, setMockRoomStatus } from '@/mocks/roomStore'
import { RoomPage, RoomsPage } from '@/features/rooms'

vi.mock('@/shared/notify', () => ({
  notify: { apiError: vi.fn<(e?: unknown) => void>(), error: vi.fn<(m: string) => void>(), success: vi.fn<(m: string) => void>(), info: vi.fn<(m: string) => void>() },
}))

afterEach(cleanup)

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>
}

function renderAt(path: string, client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })) {
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/" element={<p>Tonight</p>} />
          <Route path="/rooms" element={<RoomsPage />} />
          <Route path="/rooms/:code" element={<RoomPage />} />
          <Route path="/rooms/:code/swipe" element={<p>swipe screen</p>} />
        </Routes>
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('Movie nights', () => {
  it('creates a room and lands in its lobby with the code and the host controls', async () => {
    const user = userEvent.setup()
    renderAt('/rooms')
    await user.type(await screen.findByLabelText('Room name'), 'Friday Film Club')
    await user.click(screen.getByRole('button', { name: 'Create room' }))
    expect(await screen.findByRole('heading', { name: 'Friday Film Club' })).toBeTruthy()
    expect(screen.getByTestId('where').textContent).toMatch(/^\/rooms\/[A-Z]{3}-\d{3}$/)
    expect(screen.getByRole('button', { name: /Start swiping · \d OF 1 READY/ })).toBeTruthy()
  })

  it('shows the server\'s field error for a blank room name', async () => {
    const user = userEvent.setup()
    renderAt('/rooms')
    await user.click(await screen.findByRole('button', { name: 'Create room' }))
    expect(await screen.findByText('Use 1 to 40 characters.')).toBeTruthy()
  })

  it('offers sign-in instead of the form to a visitor, who can still enter a code', async () => {
    setMockSession(null)
    const user = userEvent.setup()
    renderAt('/rooms')
    expect(await screen.findByRole('link', { name: 'Sign in' })).toBeTruthy()
    await user.type(screen.getByLabelText('Have a room code?'), 'pop-428')
    await user.click(screen.getByRole('button', { name: 'Join' }))
    expect(screen.getByTestId('where').textContent).toBe('/rooms/POP-428')
  })
})

describe('Joining', () => {
  it('asks a visitor for a display name, validates it with the server, then joins as a guest', async () => {
    setMockSession(null)
    const user = userEvent.setup()
    renderAt(`/rooms/${MOCK_ROOM_CODE}`)
    expect(await screen.findByRole('heading', { name: 'Friday Film Club' })).toBeTruthy()
    expect(screen.getByText('Hosted by Omar')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Join as a guest' }))
    expect(await screen.findByText('Use 1 to 30 characters.')).toBeTruthy()
    await user.type(screen.getByLabelText('Your name'), 'Sam')
    await user.click(screen.getByRole('button', { name: 'Join as a guest' }))
    expect(await screen.findByRole('heading', { name: 'Your vibe for tonight' })).toBeTruthy() // mood room: answer first
    await user.click(screen.getByRole('button', { name: 'Cozy' }))
    await user.click(screen.getByRole('button', { name: "I'm ready" }))
    expect(await screen.findByText('Sam')).toBeTruthy()
    expect(screen.getByText('Guest')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Start swiping/ })).toBeNull() // host-only control
  })

  it('lets a signed-in user join with one tap', async () => {
    const user = userEvent.setup()
    renderAt(`/rooms/${MOCK_ROOM_CODE.toLowerCase()}`)
    expect(screen.queryByLabelText('Your name')).toBeNull()
    await user.click(await screen.findByRole('button', { name: 'Join the room' }))
    await user.click(await screen.findByRole('button', { name: 'Cozy' }))
    await user.click(screen.getByRole('button', { name: "I'm ready" }))
    expect(await screen.findByText('Lina')).toBeTruthy()
    expect(screen.getByText(/Waiting for Omar to start/)).toBeTruthy()
  })

  it('says so when the room does not exist', async () => {
    renderAt('/rooms/NOPE-000')
    expect((await screen.findByRole('alert')).textContent).toMatch(/couldn't find that room/)
  })

  it('does not offer to join a closed room', async () => {
    setMockRoomStatus(MOCK_ROOM_CODE, 'closed')
    renderAt(`/rooms/${MOCK_ROOM_CODE}`)
    expect((await screen.findByRole('alert')).textContent).toMatch(/closed/)
    expect(screen.queryByRole('button', { name: /Join/ })).toBeNull()
  })

  it('shows the room as full without joining', async () => {
    for (let i = 0; i < 6; i++) addMockRoomMember(MOCK_ROOM_CODE, `Friend ${i}`)
    renderAt(`/rooms/${MOCK_ROOM_CODE}`)
    expect(await screen.findByText('This room is full.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Join the room' }) as HTMLButtonElement).disabled).toBe(true)
  })
})

describe('Lobby', () => {
  async function joinAsHostOfNewRoom() {
    const user = userEvent.setup()
    renderAt('/rooms')
    await user.type(await screen.findByLabelText('Room name'), 'Club')
    await user.click(screen.getByRole('button', { name: 'Create room' }))
    await screen.findByRole('heading', { name: 'Club' })
    return { user, code: screen.getByTestId('where').textContent?.split('/').pop() ?? '' }
  }

  it('counts who is ready on the host button (mood room)', async () => {
    const user = userEvent.setup()
    renderAt('/rooms')
    await user.type(await screen.findByLabelText('Room name'), 'Club')
    await user.click(screen.getByRole('radio', { name: 'By mood' }))
    await user.click(screen.getByRole('button', { name: 'Create room' }))
    await user.click(await screen.findByRole('button', { name: "See who's here" })) // the host may skip their own answer
    expect(await screen.findByRole('button', { name: 'Start swiping · 0 OF 1 READY' })).toBeTruthy()
  })

  it('shows members with their ready state', async () => {
    renderAt(`/rooms/${MOCK_ROOM_CODE}`)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Join the room' }))
    await user.click(await screen.findByRole('button', { name: 'Cozy' }))
    await user.click(screen.getByRole('button', { name: "I'm ready" }))
    await screen.findByText('Lina')
    expect(screen.getAllByText('Ready')).toHaveLength(2) // Omar and Lina; Noor is waiting
    expect(screen.getAllByText('Waiting')).toHaveLength(1)
    expect(screen.getByText('Host')).toBeTruthy()
  })

  it('starts the room and moves on to the swipe screen', async () => {
    const { user, code } = await joinAsHostOfNewRoom()
    await user.click(screen.getByRole('button', { name: /Start swiping/ }))
    await waitFor(() => expect(screen.getByTestId('where').textContent).toBe(`/rooms/${code}/swipe`))
    expect(screen.getByText('swipe screen')).toBeTruthy()
  })

  it('picks up a member who joined and a readiness change on the next read', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    const user = userEvent.setup()
    renderAt('/rooms', client)
    await user.type(await screen.findByLabelText('Room name'), 'Club')
    await user.click(screen.getByRole('radio', { name: 'By mood' }))
    await user.click(screen.getByRole('button', { name: 'Create room' }))
    await user.click(await screen.findByRole('button', { name: "See who's here" }))
    await screen.findByRole('heading', { name: 'Club' })
    const code = screen.getByTestId('where').textContent?.split('/').pop() ?? ''
    const friend = addMockRoomMember(code, 'Noor')
    await client.invalidateQueries({ queryKey: ['rooms', 'detail', code] }) // what the hub handlers do on memberJoined
    expect(await screen.findByText('Noor')).toBeTruthy()
    setMockMemberReady(code, friend?.id ?? '', true)
    await client.invalidateQueries({ queryKey: ['rooms', 'detail', code] })
    expect(await screen.findByRole('button', { name: 'Start swiping · 1 OF 2 READY' })).toBeTruthy()
  })

  it('shows the server\'s answer when starting fails', async () => {
    const { user } = await joinAsHostOfNewRoom()
    server.use(notHostStart())
    await user.click(screen.getByRole('button', { name: /Start swiping/ }))
    expect((await screen.findByRole('alert')).textContent).toMatch(/Only the host/)
  })

  it('leaves the room and goes home', async () => {
    const { user, code } = await joinAsHostOfNewRoom()
    await user.click(screen.getByRole('button', { name: 'Leave room' }))
    await waitFor(() => expect(screen.getByTestId('where').textContent).toBe('/'))
    expect(mockRoomFor(code)).toBeUndefined()
  })
})

describe('Room mood step', () => {
  async function joinSeedRoom() {
    const user = userEvent.setup()
    renderAt(`/rooms/${MOCK_ROOM_CODE}`)
    await user.click(await screen.findByRole('button', { name: 'Join the room' }))
    await screen.findByRole('heading', { name: 'Your vibe for tonight' })
    return user
  }

  it('asks for a mood in room wording, with private hard nos', async () => {
    await joinSeedRoom()
    expect(screen.getByText('How long can you stay up?', { selector: 'span' })).toBeTruthy()
    expect(screen.getByText(/the group never sees these/)).toBeTruthy()
    expect(screen.queryByLabelText('Language')).toBeNull()
    expect(screen.getByText('1 OF 3 READY')).toBeTruthy()
  })

  it('submits the answer and moves on to the lobby with the member ready', async () => {
    const user = await joinSeedRoom()
    await user.click(screen.getByRole('button', { name: 'Cozy' }))
    await user.click(screen.getByRole('button', { name: 'Horror' }))
    await user.click(screen.getByRole('button', { name: "I'm ready" }))
    expect(await screen.findByText(/Waiting for Omar to start · 2 OF 3 READY/)).toBeTruthy()
    expect(screen.queryByText(/couldn't make sense/)).toBeNull()
  })

  it('shows mood.unclear under the text, keeps the text and stays on the step', async () => {
    const user = await joinSeedRoom()
    await user.type(screen.getByRole('textbox', { name: 'Describe it your way' }), 'asdf')
    await user.click(screen.getByRole('button', { name: "I'm ready" }))
    expect(await screen.findByText(/couldn't find a mood in that/)).toBeTruthy()
    expect((screen.getByRole('textbox', { name: 'Describe it your way' }) as HTMLTextAreaElement).value).toBe('asdf')
    expect(screen.getByRole('heading', { name: 'Your vibe for tonight' })).toBeTruthy()
  })

  it('goes on with the chips and tells the member quietly when the text was ignored', async () => {
    const user = await joinSeedRoom()
    await user.click(screen.getByRole('button', { name: 'Cozy' }))
    await user.type(screen.getByRole('textbox', { name: 'Describe it your way' }), 'asdf')
    await user.click(screen.getByRole('button', { name: "I'm ready" }))
    expect(await screen.findByText(/We went with your moods; we couldn't make sense of the text/)).toBeTruthy()
  })

  it('lets the host skip ahead to the lobby, but not other members', async () => {
    const user = userEvent.setup()
    renderAt('/rooms')
    await user.type(await screen.findByLabelText('Room name'), 'Club')
    await user.click(screen.getByRole('radio', { name: 'By mood' }))
    await user.click(screen.getByRole('button', { name: 'Create room' }))
    expect(await screen.findByRole('button', { name: "See who's here" })).toBeTruthy()
    cleanup()
    await joinSeedRoom()
    expect(screen.queryByRole('button', { name: "See who's here" })).toBeNull()
  })
})
