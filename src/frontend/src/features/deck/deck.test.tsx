// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createMockDeck, mockDeckDto, recordMockSwipe } from '@/mocks/deckStore'
import { failDeckSwipe } from '@/mocks/overrides'
import { server } from '@/mocks/server'
import { DeckPage } from './pages/DeckPage'
import { DeckStartPage } from './pages/DeckStartPage'
import { DeckTopPage } from './pages/DeckTopPage'

const notifyError = vi.hoisted(() => vi.fn<(message?: unknown) => void>())
vi.mock('@/shared/notify', () => ({
  notify: { apiError: notifyError, error: notifyError, success: vi.fn<(m: string) => void>(), info: vi.fn<(m: string) => void>() },
}))

afterEach(() => {
  cleanup()
  notifyError.mockClear()
  server.events.removeAllListeners()
})

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/deck" element={<DeckStartPage />} />
          <Route path="/deck/:deckId" element={<DeckPage />} />
          <Route path="/deck/:deckId/top" element={<DeckTopPage />} />
          <Route path="/films/:tmdbId" element={<p>film</p>} />
        </Routes>
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const card = () => screen.findByRole('article')
const title = async () => (await screen.findByRole('heading', { level: 2 })).textContent

describe('solo deck', () => {
  it('/deck starts a deck and opens it', async () => {
    renderAt('/deck')
    await waitFor(() => expect(screen.getByTestId('where').textContent).toMatch(/^\/deck\/[\w-]+$/))
    expect(await card()).toBeTruthy()
  })

  it('swipes a full batch with the keyboard, one request per swipe, then asks for more', async () => {
    const deck = createMockDeck()
    const requests: string[] = []
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'POST') requests.push(new URL(request.url).pathname.split('/').pop() ?? '')
    })
    const user = userEvent.setup()
    renderAt(`/deck/${deck.id}`)
    const first = await title()
    ;(await card()).focus()
    for (let n = 0; n < 5; n++) {
      await user.keyboard(n % 2 ? '{ArrowLeft}' : '{ArrowRight}')
      await waitFor(() => expect(mockDeckDto(deck.id)?.swipeCount).toBe(n + 1))
    }
    expect(mockDeckDto(deck.id)?.yesCount).toBe(3)
    expect(requests.filter((r) => r === 'swipes')).toHaveLength(5)
    // Near the end of the batch the next one was fetched, so the stack never ran dry.
    expect(requests).toContain('items')
    expect(await title()).not.toBe(first)
  })

  it('swipes by button, and "Seen it" asks how it was', async () => {
    const deck = createMockDeck()
    const user = userEvent.setup()
    renderAt(`/deck/${deck.id}`)
    await card()
    await user.click(screen.getByRole('button', { name: "I'd watch it" }))
    await waitFor(() => expect(mockDeckDto(deck.id)?.yesCount).toBe(1))

    await user.click(screen.getByRole('button', { name: 'Seen it' }))
    await user.click(await screen.findByRole('button', { name: 'Loved it' }))
    await waitFor(() => expect(mockDeckDto(deck.id)?.items[1]?.swipe).toEqual({ direction: 'up', seenVerdict: 'loved' }))
    expect(screen.queryByRole('button', { name: 'Loved it' })).toBeNull()
  })

  it('closing the sheet leaves the card in place', async () => {
    const deck = createMockDeck()
    const user = userEvent.setup()
    renderAt(`/deck/${deck.id}`)
    const first = await title()
    await user.click(screen.getByRole('button', { name: 'Seen it' }))
    await user.click(await screen.findByRole('button', { name: 'Close' }))
    expect(await title()).toBe(first)
    expect(mockDeckDto(deck.id)?.swipeCount).toBe(0)
  })

  it('a failed swipe toasts and the card comes back', async () => {
    const deck = createMockDeck()
    server.use(failDeckSwipe())
    const user = userEvent.setup()
    renderAt(`/deck/${deck.id}`)
    const first = await title()
    await user.click(screen.getByRole('button', { name: "I'd watch it" }))
    await waitFor(() => expect(notifyError).toHaveBeenCalledTimes(1))
    expect(await title()).toBe(first)
    expect(screen.getByText('0 swipes · 0 yeses')).toBeTruthy()
  })

  it('the same swipeId twice counts once; another id for a swiped card is refused', () => {
    const deck = createMockDeck()
    const itemId = mockDeckDto(deck.id)?.items[0]?.id ?? ''
    const swipe = { swipeId: '11111111-1111-4111-8111-111111111111', deckItemId: itemId, direction: 'right', seenVerdict: null } as const
    expect(recordMockSwipe(deck.id, swipe)).toBe('ok')
    expect(recordMockSwipe(deck.id, swipe)).toBe('ok')
    expect(mockDeckDto(deck.id)?.swipeCount).toBe(1)
    expect(recordMockSwipe(deck.id, { ...swipe, swipeId: '22222222-2222-4222-8222-222222222222' })).toBe('already_swiped')
  })

  it('the watchlist button is separate from the swipe', async () => {
    const deck = createMockDeck()
    const user = userEvent.setup()
    renderAt(`/deck/${deck.id}`)
    await card()
    const save = screen.getByRole('button', { name: /to your watchlist/ })
    await user.click(save)
    await waitFor(() => expect(save.getAttribute('aria-pressed')).toBe('true'))
    expect(mockDeckDto(deck.id)?.items[0]?.onWatchlist).toBe(true)
    expect(mockDeckDto(deck.id)?.swipeCount).toBe(0)
  })

  it('finishes the deck and shows the top 3 (1 to 3 picks)', async () => {
    const deck = createMockDeck()
    const user = userEvent.setup()
    renderAt(`/deck/${deck.id}`)
    await card()
    // Say yes to everything: 12 films, three batches.
    for (let n = 0; n < 12; n++) {
      await user.click(await screen.findByRole('button', { name: "I'd watch it" }))
      await waitFor(() => expect(mockDeckDto(deck.id)?.swipeCount).toBe(n + 1))
    }
    expect(await screen.findByText("That's the whole deck")).toBeTruthy()
    await user.click(screen.getByRole('link', { name: 'See your top 3' }))
    expect(await screen.findByRole('heading', { name: 'Your top 3' })).toBeTruthy()
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
  })

  it('top 3 with no yeses shows the deck.no_yeses state', async () => {
    const deck = createMockDeck()
    renderAt(`/deck/${deck.id}/top`)
    expect(await screen.findByText('No yeses yet')).toBeTruthy()
    expect(screen.getByText(/Swipe right on a few films first/)).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Back to the deck' })).toBeTruthy()
  })

  it('top 3 shows a single pick after one yes', async () => {
    const deck = createMockDeck()
    const user = userEvent.setup()
    renderAt(`/deck/${deck.id}`)
    await card()
    await user.click(screen.getByRole('button', { name: "I'd watch it" }))
    await waitFor(() => expect(mockDeckDto(deck.id)?.yesCount).toBe(1))
    cleanup()
    renderAt(`/deck/${deck.id}/top`)
    expect(await screen.findByRole('heading', { name: 'Your top 1' })).toBeTruthy()
  })
})
