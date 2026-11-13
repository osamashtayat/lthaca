// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { setMockSession } from '@/mocks/authStore'
import { apiPath, problem } from '@/mocks/http'
import { server } from '@/mocks/server'
import { FilmPage } from './pages/FilmPage'

const notifyError = vi.hoisted(() => vi.fn())
vi.mock('@/shared/notify', () => ({
  notify: { apiError: notifyError, error: notifyError, success: vi.fn(), info: vi.fn() },
}))

afterEach(() => {
  cleanup()
  notifyError.mockClear()
})

function Where() {
  const l = useLocation()
  return <p data-testid="where">{`${l.pathname}${l.search}`}</p>
}

function renderFilm(path = '/films/550', client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })) {
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/films/:tmdbId" element={<FilmPage />} />
          <Route path="/sign-in" element={<p>sign in</p>} />
        </Routes>
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const chip = (name: string) => screen.findByRole('button', { name: new RegExp(name) })

describe('FilmPage', () => {
  it('shows the film facts', async () => {
    renderFilm()
    expect(await screen.findByRole('heading', { name: 'Fight Club' })).toBeTruthy()
    expect(screen.getByText('1999 · 139 min')).toBeTruthy()
    expect(screen.getByText('8.4')).toBeTruthy()
    expect(screen.getByText('Drama')).toBeTruthy()
  })

  it('toggles Like and keeps it across a reload', async () => {
    const user = userEvent.setup()
    renderFilm()
    const like = await chip('Like')
    await waitFor(() => expect((like as HTMLButtonElement).disabled).toBe(false))
    await user.click(like)
    await waitFor(() => expect(like.getAttribute('aria-pressed')).toBe('true'))

    cleanup() // a "reload": new QueryClient, same mock server state
    renderFilm()
    const again = await chip('Like')
    await waitFor(() => expect(again.getAttribute('aria-pressed')).toBe('true'))
  })

  it('rolls back and toasts when the PUT fails', async () => {
    server.use(http.put(apiPath('/api/library/{tmdbId}'), () => problem(500, 'server.error')))
    const user = userEvent.setup()
    renderFilm()
    const saved = await chip('Saved')
    await waitFor(() => expect((saved as HTMLButtonElement).disabled).toBe(false))
    await user.click(saved)
    await waitFor(() => expect(notifyError).toHaveBeenCalledTimes(1))
    expect(saved.getAttribute('aria-pressed')).toBe('false')
  })

  it('rates with the keyboard, which also marks the film seen', async () => {
    const user = userEvent.setup()
    renderFilm()
    const group = await screen.findByRole('radiogroup', { name: 'Your rating' })
    await waitFor(() => expect((screen.getByRole('radio', { name: '1 of 5' }) as HTMLInputElement).disabled).toBe(false))
    await user.tab() // walk to the rating group's single tab stop
    while (!group.contains(document.activeElement)) await user.tab()
    await user.keyboard('{ArrowRight}{ArrowRight}')
    await waitFor(() => expect((screen.getByRole('radio', { name: '3 of 5' }) as HTMLInputElement).checked).toBe(true))
    await waitFor(async () => expect((await chip('Seen it')).getAttribute('aria-pressed')).toBe('true'))
  })

  it('sends a visitor to /sign-in with a way back', async () => {
    setMockSession(null)
    const user = userEvent.setup()
    renderFilm()
    await user.click(await chip('Like'))
    await waitFor(() => expect(screen.getByTestId('where').textContent).toBe('/sign-in?next=%2Ffilms%2F550'))
  })

  it('opens the trailer in a privacy-enhanced embed, or says there is none', async () => {
    const user = userEvent.setup()
    renderFilm()
    await user.click(await chip('Trailer'))
    const frame = await screen.findByTitle('Fight Club — Trailer')
    expect(frame.getAttribute('src')).toContain('youtube-nocookie.com/embed/')

    cleanup()
    renderFilm('/films/496243')
    await user.click(await chip('Trailer'))
    expect(await screen.findByText('No trailer for this film yet.')).toBeTruthy()
  })

  it('shows the not-found message for an unknown film', async () => {
    server.use(http.get(apiPath('/api/films/{tmdbId}'), () => HttpResponse.json({ code: 'film.not_found', status: 404, traceId: 't' }, { status: 404 })))
    renderFilm('/films/1')
    expect(await screen.findByRole('alert')).toBeTruthy()
  })
})
