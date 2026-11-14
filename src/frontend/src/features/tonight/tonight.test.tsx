// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { setMockSession } from '@/mocks/authStore'
import { dayLabelFor, greetingFor } from './greeting'
import { TonightPage } from './pages/TonightPage'

afterEach(cleanup)

function Where() {
  const l = useLocation()
  return <p data-testid="where">{l.pathname + l.search}</p>
}

const FRIDAY_EVENING = new Date(2026, 0, 2, 20, 0)

function renderHome() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <Routes>
          <Route path="*" element={<TonightPage now={FRIDAY_EVENING} />} />
        </Routes>
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const href = (name: string) => screen.getByRole('link', { name }).getAttribute('href')

describe('greeting', () => {
  it('follows the hour and names the day', () => {
    expect(greetingFor(new Date(2026, 0, 2, 8))).toBe('Good morning')
    expect(greetingFor(new Date(2026, 0, 2, 14))).toBe('Good afternoon')
    expect(greetingFor(new Date(2026, 0, 2, 23))).toBe('Good evening')
    expect(dayLabelFor(FRIDAY_EVENING)).toBe('FRIDAY NIGHT')
  })
})

describe('Tonight (signed in)', () => {
  it('greets the user and links straight to the deck, mood and rooms', async () => {
    const user = userEvent.setup()
    renderHome()
    expect(await screen.findByRole('heading', { name: 'Good evening, Lina' })).toBeTruthy()
    expect(href('Just swipe')).toBe('/deck')
    expect(href('Pick a mood')).toBe('/mood')
    await user.click(screen.getByRole('radio', { name: 'With friends' }))
    expect(href('Start a room')).toBe('/rooms')
  })

  it('links By taste to /taste', async () => {
    renderHome()
    await screen.findByRole('heading', { name: /Good evening/ })
    expect(href('Build from my taste')).toBe('/taste')
  })
})

describe('Tonight (loading)', () => {
  it('shows the page before the session answers', () => {
    renderHome()
    expect(screen.getByRole('heading', { name: 'Good evening' })).toBeTruthy()
    expect(href('Just swipe')).toBe('/deck')
  })
})

describe('Tonight (visitor)', () => {
  it('toggles, and sends every action to sign-in with the way back', async () => {
    setMockSession(null)
    const user = userEvent.setup()
    renderHome()
    await waitFor(() => expect(href('Just swipe')).toBe('/sign-in?next=%2Fdeck'))
    expect(href('Pick a mood')).toBe('/sign-in?next=%2Fmood')
    expect(href('Build from my taste')).toBe('/sign-in?next=%2Ftaste')
    await user.click(screen.getByRole('radio', { name: 'With friends' }))
    expect(href('Start a room')).toBe('/sign-in?next=%2Frooms')
  })

  it('can still join by code', async () => {
    setMockSession(null)
    const user = userEvent.setup()
    renderHome()
    await user.type(await screen.findByLabelText('Room code'), 'POP-428')
    await user.click(screen.getByRole('button', { name: 'Join' }))
    expect(screen.getByTestId('where').textContent).toBe('/rooms/POP-428')
  })
})
