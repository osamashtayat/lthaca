// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { RequireUser } from '@/features/auth'
import { setMockSession } from '@/mocks/authStore'
import { mockFilms } from '@/mocks/fixtures'
import { putMockUserFilm } from '@/mocks/libraryStore'
import { CinemaPage } from './pages/CinemaPage'

afterEach(cleanup)

function Where() {
  const l = useLocation()
  return <p data-testid="where">{`${l.pathname}${l.search}`}</p>
}

function renderCinema(path = '/cinema') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<RequireUser />}>
            <Route path="/cinema" element={<CinemaPage />} />
          </Route>
          <Route path="/sign-in" element={<p>sign in</p>} />
        </Routes>
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const state = (patch: Partial<{ liked: boolean; onWatchlist: boolean; seen: boolean; rating: number | null }>) => ({
  liked: false, onWatchlist: false, seen: false, rating: null, ...patch,
})
const titles = () => within(screen.getByRole('list')).getAllByRole('link').map((a) => a.textContent ?? '')

describe('My Cinema', () => {
  it('shows the empty state on a fresh account', async () => {
    renderCinema()
    expect(await screen.findByText('Nothing on the reel yet')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Import from Letterboxd' }).getAttribute('href')).toBe('/import')
  })

  it('switches shelves and shows the counts', async () => {
    putMockUserFilm(550, state({ liked: true }))
    putMockUserFilm(27205, state({ onWatchlist: true }))
    putMockUserFilm(496243, state({ onWatchlist: true, seen: true }))
    const user = userEvent.setup()
    renderCinema()
    expect(await screen.findByText('Fight Club')).toBeTruthy()
    expect(screen.getByLabelText('Liked 1')).toBeTruthy()
    expect(screen.getByLabelText('Saved 2')).toBeTruthy()
    expect(screen.getByLabelText('Seen 1')).toBeTruthy()

    await user.click(screen.getByLabelText('Saved 2'))
    await waitFor(() => expect(titles().join()).toContain('Parasite'))
    expect(titles().join()).toContain('Inception')
    expect(titles().join()).not.toContain('Fight Club')

    await user.click(screen.getByLabelText('Seen 1'))
    await waitFor(() => expect(titles()).toHaveLength(1))
    expect(screen.getByTestId('where').textContent).toBe('/cinema?shelf=seen')
  })

  it('filters by genre and sorts', async () => {
    putMockUserFilm(550, state({ liked: true, rating: 2 }))
    putMockUserFilm(27205, state({ liked: true, rating: 5 }))
    putMockUserFilm(496243, state({ liked: true }))
    const user = userEvent.setup()
    renderCinema()
    await screen.findByText('Parasite')
    expect(titles()[0]).toContain('Parasite') // most recently added first

    await user.selectOptions(screen.getByLabelText('Sort'), 'rating')
    await waitFor(() => expect(titles()[0]).toContain('Inception'))
    await user.selectOptions(screen.getByLabelText('Sort'), 'title')
    await waitFor(() => expect(titles()[0]).toContain('Fight Club'))

    await screen.findByRole('option', { name: 'Science Fiction' })
    await user.selectOptions(screen.getByLabelText('Genre'), 'Science Fiction')
    await waitFor(() => expect(titles()).toHaveLength(1))
    expect(titles()[0]).toContain('Inception')
  })

  it('pages with load more', async () => {
    for (const film of mockFilms) putMockUserFilm(film.tmdbId, state({ liked: true }))
    const user = userEvent.setup()
    renderCinema()
    await screen.findByRole('list')
    expect(titles()).toHaveLength(9)
    await user.click(screen.getByRole('button', { name: 'Load more' }))
    await waitFor(() => expect(titles()).toHaveLength(mockFilms.length))
    expect(screen.queryByRole('button', { name: 'Load more' })).toBeNull()
  })

  it('is user-only', async () => {
    setMockSession(null)
    renderCinema('/cinema?shelf=seen')
    await screen.findByText('sign in')
    expect(screen.getByTestId('where').textContent).toBe('/sign-in?next=%2Fcinema%3Fshelf%3Dseen')
  })
})
