// @vitest-environment jsdom
import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { MOCK_GUEST, MOCK_PASSWORD, getMockSession, setMockSession } from '@/mocks/authStore'
import { scriptMockImport } from '@/mocks/importStore'
import { setMockTaste } from '@/mocks/tasteStore'
import { createQueryClient } from '@/shared/query'
import { RequireUser } from '@/features/auth'
import { ProfilePage } from './pages/ProfilePage'

afterEach(() => {
  cleanup()
  setMockTaste(null)
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT'
})

function renderProfile() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={['/profile']}>
        <Routes>
          <Route element={<RequireUser />}>
            <Route path="/profile" element={<ProfilePage />} />
          </Route>
          <Route path="/import" element={<p>import page</p>} />
          <Route path="/sign-in" element={<p>sign-in page</p>} />
          <Route path="/" element={<p>Tonight</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('ProfilePage', () => {
  it('shows the name, email and join month', async () => {
    renderProfile()
    expect(await screen.findByRole('heading', { name: 'Lina' })).toBeTruthy()
    expect(screen.getByText('lina@example.com')).toBeTruthy()
    expect(screen.getByText('SINCE NOV 2026')).toBeTruthy()
  })

  it('sends a guest to sign in', async () => {
    setMockSession(MOCK_GUEST)
    renderProfile()
    expect(await screen.findByText('sign-in page')).toBeTruthy()
  })

  it('saves a new display name and shows a field error for a blank one', async () => {
    const user = userEvent.setup()
    renderProfile()
    const input = await screen.findByLabelText('Name')
    await user.clear(input)
    await user.type(input, '   ')
    await user.click(screen.getByRole('button', { name: 'Save name' }))
    expect(await screen.findByText('Use 1 to 40 characters.')).toBeTruthy()

    await user.clear(input)
    await user.type(input, 'Lina M')
    await user.click(screen.getByRole('button', { name: 'Save name' }))
    expect(await screen.findByRole('heading', { name: 'Lina M' })).toBeTruthy()
    expect(getMockSession()?.displayName).toBe('Lina M')
  })

  it('shows the taste chips when ready, else the progress', async () => {
    setMockTaste({ ready: true, signalCount: 12, signalsNeeded: 10, loggedCount: 30, topGenres: [{ genre: 'Drama', percent: 40 }], notForYou: ['Horror'] })
    renderProfile()
    expect(await screen.findByText('Drama 40%')).toBeTruthy()
    expect(screen.getByText('Not for you: Horror')).toBeTruthy()
    cleanup()
    setMockTaste({ ready: false, signalCount: 3, signalsNeeded: 10, loggedCount: 3, topGenres: [], notForYou: [] })
    renderProfile()
    expect(await screen.findByText(/3 of 10 films to read your taste/)).toBeTruthy()
  })

  it('invites to import when there is none, and links to /import', async () => {
    const user = userEvent.setup()
    renderProfile()
    await user.click(await screen.findByRole('link', { name: 'Import your history' }))
    expect(await screen.findByText('import page')).toBeTruthy()
  })

  it('shows the latest import read-only', async () => {
    scriptMockImport({ status: 'completed', finishedAt: '2026-11-13T12:00:00Z', matchedCount: 1160, notFoundCount: 45 })
    renderProfile()
    expect(await screen.findByText('Nov 13, 2026 · 1,160 matched · 45 not found')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Import again' })).toBeTruthy()
  })

  it('changes the password: a wrong current one is refused, the right one closes the sheet', async () => {
    const user = userEvent.setup()
    renderProfile()
    await user.click(await screen.findByRole('button', { name: 'Change password' }))
    const sheet = screen.getByRole('dialog', { name: 'Change password' })
    await user.type(within(sheet).getByLabelText('Current password'), 'nope-nope')
    await user.type(within(sheet).getByLabelText('New password'), 'brand-new-pass')
    await user.click(within(sheet).getByRole('button', { name: 'Change password' }))
    expect(await within(sheet).findByRole('alert')).toBeTruthy()
    expect(within(sheet).getByRole('alert').textContent).toContain("password isn't right")

    await user.clear(within(sheet).getByLabelText('Current password'))
    await user.type(within(sheet).getByLabelText('Current password'), MOCK_PASSWORD)
    await user.click(within(sheet).getByRole('button', { name: 'Change password' }))
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Change password' })).toBeNull())
  })

  it('deletes the account with the password and leaves for Tonight signed out', async () => {
    const user = userEvent.setup()
    renderProfile()
    await user.click(await screen.findByRole('button', { name: 'Delete account' }))
    const sheet = screen.getByRole('dialog', { name: 'Delete account' })
    await user.type(within(sheet).getByLabelText('Your password'), 'wrong-pass')
    await user.click(within(sheet).getByRole('button', { name: 'Delete my account' }))
    expect((await within(sheet).findByRole('alert')).textContent).toContain("password isn't right")
    expect(getMockSession()).not.toBeNull()

    await user.clear(within(sheet).getByLabelText('Your password'))
    await user.type(within(sheet).getByLabelText('Your password'), MOCK_PASSWORD)
    await user.click(within(sheet).getByRole('button', { name: 'Delete my account' }))
    expect(await screen.findByText('Tonight')).toBeTruthy()
    expect(getMockSession()).toBeNull()
  })
})
