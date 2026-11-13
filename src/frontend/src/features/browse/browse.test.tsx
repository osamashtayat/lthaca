// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider, useLocation } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { setMockSession } from '@/mocks/authStore'
import { mockFilms } from '@/mocks/fixtures'
import { putMockUserFilm } from '@/mocks/libraryStore'
import { server } from '@/mocks/server'
import { BrowsePage } from './pages/BrowsePage'

afterEach(() => {
  cleanup()
  server.events.removeAllListeners()
})

function Where() {
  const l = useLocation()
  return <p data-testid="where">{`${l.pathname}${l.search}`}</p>
}

function renderBrowse(path = '/browse') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [{ path: '/browse', element: (<><BrowsePage /><Where /></>) }, { path: '/films/:id', element: <p>film</p> }],
    { initialEntries: [path] },
  )
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

const resultTitles = () => screen.queryAllByRole('link').map((a) => a.textContent ?? '')
const where = () => screen.getByTestId('where').textContent

describe('Browse', () => {
  it('starts as "Trending this week" in catalog order, paged', async () => {
    const user = userEvent.setup()
    renderBrowse()
    expect(await screen.findByRole('heading', { name: 'Trending this week' })).toBeTruthy()
    await screen.findByText('Fight Club')
    expect(resultTitles().filter((t) => mockFilms.some((f) => t.includes(f.title)))).toHaveLength(8)
    await user.click(screen.getByRole('button', { name: 'Load more' }))
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Load more' })).toBeNull())
    expect(screen.getByText('Spirited Away')).toBeTruthy()
  })

  it('chips send the contract query and update the URL', async () => {
    const requests: URL[] = []
    server.events.on('request:start', ({ request }) => {
      const url = new URL(request.url)
      if (url.pathname === '/api/films') requests.push(url)
    })
    const user = userEvent.setup()
    renderBrowse()
    await screen.findByText('Fight Club')
    await user.click(screen.getByRole('button', { name: 'Under 2h' }))
    await user.click(screen.getByRole('button', { name: '★ 7+' }))
    await user.click(await screen.findByRole('button', { name: 'Hide seen' }))
    await waitFor(() => expect(where()).toBe('/browse?under2h=1&hideSeen=1&topRated=1'))
    await waitFor(() => {
      const last = requests.at(-1)?.searchParams
      expect(last?.get('maxRuntime')).toBe('upTo2h')
      expect(last?.get('minVoteAverage')).toBe('7')
      expect(last?.get('hideSeen')).toBe('true')
      expect(last?.get('sort')).toBe('popular')
    })
    expect(await screen.findByRole('heading', { name: 'Results' })).toBeTruthy()
    // Long films are gone (Schindler's List runs 195 minutes)
    expect(screen.queryByText("Schindler's List")).toBeNull()
  })

  it('searches (debounced) and the URL carries it', async () => {
    const user = userEvent.setup()
    renderBrowse()
    await screen.findByText('Fight Club')
    await user.type(screen.getByRole('searchbox', { name: 'Search' }), 'incep')
    await waitFor(() => expect(where()).toBe('/browse?q=incep'))
    await waitFor(() => expect(resultTitles().filter((t) => t.includes('Inception'))).toHaveLength(1))
    expect(screen.queryByText('Fight Club')).toBeNull()
  })

  it('genre tiles filter, and Back restores the previous filters', async () => {
    const user = userEvent.setup()
    const router = renderBrowse()
    await screen.findByText('Fight Club')
    const tile = await screen.findByRole('button', { name: 'Animation' })
    await user.click(tile)
    await waitFor(() => expect(where()).toBe('/browse?genre=Animation'))
    await waitFor(() => expect(screen.queryByText('Fight Club')).toBeNull())
    expect(screen.getByText('Spirited Away')).toBeTruthy()
    expect(tile.getAttribute('aria-pressed')).toBe('true')

    await router.navigate(-1)
    await waitFor(() => expect(where()).toBe('/browse'))
    expect(await screen.findByText('Fight Club')).toBeTruthy()
  })

  it('opens from a URL with filters, and the box shows the search', async () => {
    renderBrowse('/browse?q=parasite&genre=Drama')
    expect(await screen.findByText('Parasite')).toBeTruthy()
    expect((screen.getByRole('searchbox', { name: 'Search' }) as HTMLInputElement).value).toBe('parasite')
  })

  it('"Hide seen" hides films the user has seen', async () => {
    putMockUserFilm(550, { liked: false, onWatchlist: false, seen: true, rating: null })
    renderBrowse('/browse?hideSeen=1')
    await screen.findByText('Inception')
    expect(screen.queryByText('Fight Club')).toBeNull()
  })

  it('offers "Hide seen" only to signed-in users', async () => {
    setMockSession(null)
    renderBrowse()
    await screen.findByText('Fight Club')
    expect(screen.queryByRole('button', { name: 'Hide seen' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Under 2h' })).toBeTruthy()
  })

  it('shows an empty state with a way out', async () => {
    const user = userEvent.setup()
    renderBrowse('/browse?q=zzzz')
    expect(await screen.findByText('No films match that.')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    await waitFor(() => expect(where()).toBe('/browse'))
    expect(await screen.findByText('Fight Club')).toBeTruthy()
  })
})
