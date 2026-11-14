// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { MOCK_SIGNALS_NEEDED, setMockTaste } from '@/mocks/tasteStore'
import { putMockUserFilm } from '@/mocks/libraryStore'
import { mockFilms } from '@/mocks/fixtures'
import { server } from '@/mocks/server'
import { notReadyTasteDeck } from '@/mocks/overrides'
import { TastePage } from './pages/TastePage'

afterEach(cleanup)

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>
}

function renderTaste() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/taste']}>
        <Routes>
          <Route path="/taste" element={<TastePage />} />
          <Route path="/import" element={<p>import screen</p>} />
          <Route path="/deck" element={<p>just swipe</p>} />
          <Route path="/deck/:deckId" element={<p>the deck</p>} />
          <Route path="/browse" element={<p>browse</p>} />
        </Routes>
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const READY = {
  ready: true,
  signalCount: 14,
  signalsNeeded: 10,
  loggedCount: 214,
  topGenres: [
    { genre: 'Drama', percent: 41 },
    { genre: 'Thriller', percent: 22 },
  ],
  notForYou: ['Horror', 'Heists'],
}

describe('By taste, not ready', () => {
  it('shows "Import your Letterboxd" instead of a deck, with where the user stands', async () => {
    renderTaste()
    expect(await screen.findByRole('heading', { name: 'Import your Letterboxd' })).toBeTruthy()
    expect(screen.getByText('We need a few films you love first.')).toBeTruthy()
    expect(screen.getByText('0/10')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Deal my deck' })).toBeNull()
  })

  it('both buttons navigate: Import → /import, Just swipe instead → /deck', async () => {
    const user = userEvent.setup()
    renderTaste()
    await user.click(await screen.findByRole('link', { name: 'Import from Letterboxd' }))
    expect(screen.getByTestId('where').textContent).toBe('/import')
    cleanup()
    renderTaste()
    await user.click(await screen.findByRole('link', { name: 'Just swipe instead' }))
    expect(screen.getByTestId('where').textContent).toBe('/deck')
  })

  it('the "?" opens the export steps and closing it gives focus back to the button', async () => {
    const user = userEvent.setup()
    renderTaste()
    const help = await screen.findByRole('button', { name: 'How to get your Letterboxd file' })
    await user.click(help)
    expect(await screen.findByRole('heading', { name: 'Get your Letterboxd file' })).toBeTruthy()
    expect(screen.getByText(/desktop browser view/)).toBeTruthy()
    expect(screen.getByText(/We never keep the file/)).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Close' }))
    await waitFor(() => expect(document.activeElement).toBe(help))
  })

  it('decides from the API alone: ten loved films in the mock library make it ready', async () => {
    for (const film of mockFilms.slice(0, MOCK_SIGNALS_NEEDED)) {
      putMockUserFilm(film.tmdbId, { liked: true, onWatchlist: false, seen: true, rating: null })
    }
    renderTaste()
    expect(await screen.findByRole('button', { name: 'Deal my deck' })).toBeTruthy()
  })
})

describe('By taste, ready', () => {
  it('shows the taste receipt and deals a taste deck', async () => {
    setMockTaste(READY)
    const user = userEvent.setup()
    renderTaste()
    expect(await screen.findByText("From 214 films you've logged")).toBeTruthy()
    expect(screen.getByText('Drama')).toBeTruthy()
    expect(screen.getByText('41%')).toBeTruthy()
    expect(screen.getByText('Not for you: Horror, Heists')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Deal my deck' }))
    await waitFor(() => expect(screen.getByTestId('where').textContent).toMatch(/^\/deck\/.+/))
  })

  it('falls back to the import screen when the server says taste.not_ready at deal time', async () => {
    setMockTaste(READY)
    server.use(notReadyTasteDeck())
    const user = userEvent.setup()
    renderTaste()
    const deal = await screen.findByRole('button', { name: 'Deal my deck' })
    setMockTaste({ ...READY, ready: false, signalCount: 9, topGenres: [], notForYou: [] }) // what the refetch will answer
    await user.click(deal)
    expect(await screen.findByRole('heading', { name: 'Import your Letterboxd' })).toBeTruthy()
  })
})
