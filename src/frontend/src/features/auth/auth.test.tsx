// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { MOCK_GUEST, setMockSession } from '@/mocks/authStore'
import { RequireUser } from './components/RequireUser'
import { SessionAction } from './components/SessionAction'
import { SignInPage } from './pages/SignInPage'

afterEach(cleanup)

function Where() {
  const l = useLocation()
  return <p data-testid="where">{`${l.pathname}${l.search}`}</p>
}

function renderAt(path: string, ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        {ui}
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const guarded = (
  <Routes>
    <Route element={<RequireUser />}>
      <Route path="/cinema" element={<p>cinema</p>} />
    </Route>
    <Route path="/sign-in" element={<SignInPage />} />
    <Route path="/" element={<p>home</p>} />
  </Routes>
)

describe('RequireUser', () => {
  it('lets a signed-in user through', async () => {
    renderAt('/cinema', guarded)
    expect(await screen.findByText('cinema')).toBeTruthy()
  })

  it('redirects a visitor to /sign-in?next=…', async () => {
    setMockSession(null)
    renderAt('/cinema?shelf=liked', guarded)
    await screen.findByRole('heading', { name: 'Welcome' })
    expect(screen.getByTestId('where').textContent).toBe('/sign-in?next=%2Fcinema%3Fshelf%3Dliked')
  })

  it('treats a guest as signed out', async () => {
    setMockSession(MOCK_GUEST)
    renderAt('/cinema', guarded)
    await screen.findByRole('heading', { name: 'Welcome' })
    expect(screen.getByTestId('where').textContent).toContain('/sign-in?next=')
  })
})

describe('SignInPage', () => {
  it('shows the invalid-credentials message', async () => {
    setMockSession(null)
    renderAt('/sign-in', guarded)
    await userEvent.type(await screen.findByLabelText('Email'), 'lina@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'wrong-password')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect((await screen.findByRole('alert')).textContent).toContain("don't match")
  })

  it('shows 400 field errors under the fields', async () => {
    setMockSession(null)
    renderAt('/sign-in', guarded)
    await userEvent.type(await screen.findByLabelText('Email'), 'nope')
    await userEvent.type(screen.getByLabelText('Password'), 'short')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    await waitFor(() => expect(screen.getByLabelText('Email').getAttribute('aria-invalid')).toBe('true'))
    expect(screen.getByLabelText('Password').getAttribute('aria-invalid')).toBe('true')
  })

  it('signs in and follows ?next=', async () => {
    setMockSession(null)
    renderAt('/sign-in?next=%2Fcinema', guarded)
    await userEvent.type(await screen.findByLabelText('Email'), 'lina@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'password123')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('cinema')).toBeTruthy()
  })

  it('creates an account', async () => {
    setMockSession(null)
    renderAt('/sign-in', guarded)
    await userEvent.click(await screen.findByRole('radio', { name: 'Create account' }))
    await userEvent.type(screen.getByLabelText('Name'), 'Sam')
    await userEvent.type(screen.getByLabelText('Email'), 'sam@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'password123')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))
    expect(await screen.findByText('home')).toBeTruthy()
  })

  it('reports a taken email', async () => {
    setMockSession(null)
    renderAt('/sign-in', guarded)
    await userEvent.click(await screen.findByRole('radio', { name: 'Create account' }))
    await userEvent.type(screen.getByLabelText('Name'), 'Lina')
    await userEvent.type(screen.getByLabelText('Email'), 'lina@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'password123')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))
    expect((await screen.findByRole('alert')).textContent).toContain('already has an account')
  })

  it('joins a room by code', async () => {
    setMockSession(null)
    renderAt('/sign-in', <Routes><Route path="/sign-in" element={<SignInPage />} /><Route path="/rooms/:code" element={<p>room</p>} /></Routes>)
    await userEvent.type(await screen.findByLabelText('Room code'), 'abcd')
    await userEvent.click(screen.getByRole('button', { name: 'Join' }))
    expect(await screen.findByText('room')).toBeTruthy()
    expect(screen.getByTestId('where').textContent).toBe('/rooms/abcd')
  })
})

describe('SessionAction', () => {
  it('offers Sign out to a user and Sign in after signing out', async () => {
    renderAt('/', <SessionAction />)
    await userEvent.click(await screen.findByRole('button', { name: /Sign out/ }))
    expect(await screen.findByRole('link', { name: 'Sign in' })).toBeTruthy()
  })
})
