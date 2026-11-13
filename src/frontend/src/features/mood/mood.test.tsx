// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockDeckDto } from '@/mocks/deckStore'
import { slowDeckCreate } from '@/mocks/overrides'
import { server } from '@/mocks/server'
import { DeckPage } from '@/features/deck'
import { MoodPage } from './pages/MoodPage'

vi.mock('@/shared/notify', () => ({
  notify: { apiError: vi.fn<(e?: unknown) => void>(), error: vi.fn<(m: string) => void>(), success: vi.fn<(m: string) => void>(), info: vi.fn<(m: string) => void>() },
}))

afterEach(cleanup)

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>
}

function renderMood() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/mood']}>
        <Routes>
          <Route path="/mood" element={<MoodPage />} />
          <Route path="/deck/:deckId" element={<DeckPage />} />
        </Routes>
        <Where />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const chip = (name: string) => screen.getByRole('button', { name })
const textbox = () => screen.getByRole('textbox', { name: 'Describe it your way' }) as HTMLTextAreaElement
const deal = () => screen.getByRole('button', { name: 'Deal my deck' })

async function typeAndDeal(text: string) {
  const user = userEvent.setup()
  renderMood()
  await user.click(chip('Cozy'))
  await user.type(textbox(), text)
  await user.click(deal())
  return user
}

describe('By mood', () => {
  it('lets you pick at most two moods', async () => {
    const user = userEvent.setup()
    renderMood()
    await user.click(chip('Cozy'))
    await user.click(chip('Make me laugh'))
    await user.click(chip('Surprise me'))
    expect(chip('Cozy').getAttribute('aria-pressed')).toBe('true')
    expect(chip('Make me laugh').getAttribute('aria-pressed')).toBe('true')
    expect(chip('Surprise me').getAttribute('aria-pressed')).toBe('false')
    await user.click(chip('Cozy')) // free a slot
    await user.click(chip('Surprise me'))
    expect(chip('Surprise me').getAttribute('aria-pressed')).toBe('true')
  })

  it('caps the text at 280 characters and counts them', async () => {
    const user = userEvent.setup()
    renderMood()
    expect(textbox().maxLength).toBe(280)
    await user.type(textbox(), 'abc')
    expect(screen.getByText('3 / 280')).toBeTruthy()
  })

  it('deals a mood deck and opens it', async () => {
    const user = userEvent.setup()
    renderMood()
    await user.click(chip('Cozy'))
    await user.click(screen.getByLabelText('Up to 2h'))
    await user.click(deal())
    await waitFor(() => expect(screen.getByTestId('where').textContent).toMatch(/^\/deck\//))
    const id = screen.getByTestId('where').textContent?.split('/').pop() ?? ''
    expect(mockDeckDto(id)?.mood).toMatchObject({ moods: ['cozy'], maxRuntime: 'upTo2h' })
    expect(mockDeckDto(id)?.source).toBe('mood')
  })

  it('shows a loading state while the deck is being made', async () => {
    server.use(slowDeckCreate(100))
    const user = userEvent.setup()
    renderMood()
    await user.click(deal())
    expect(await screen.findByText('Reading your mood…')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Dealing…' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it.each([
    ['refuse', "We can't help with that request."],
    ['invalid', "We couldn't come up with picks this time."],
    ['offline', 'Our movie brain is taking a break.'],
  ])('shows the message for an "%s" failure, without repeating the text', async (text, message) => {
    await typeAndDeal(text)
    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain(message)
    expect(alert.textContent).not.toContain(text)
  })

  it('mood.unclear shows under the text field, keeps the text and focuses it', async () => {
    const user = userEvent.setup()
    renderMood()
    await user.type(textbox(), 'asdf asdf')
    await user.click(deal())
    const message = await screen.findByText(/We couldn't find a mood in that/)
    expect(textbox().getAttribute('aria-describedby')).toBe(message.id)
    expect(message.textContent).not.toContain('asdf')
    expect(textbox().value).toBe('asdf asdf')
    await waitFor(() => expect(document.activeElement).toBe(textbox()))
  })

  it('textIgnored: the deck shows a note that goes after the first swipe', async () => {
    const user = await typeAndDeal('asdf')
    expect(await screen.findByText(/We went with your moods/)).toBeTruthy()
    await user.click(await screen.findByRole('button', { name: "I'd watch it" }))
    await waitFor(() => expect(screen.queryByText(/We went with your moods/)).toBeNull())
  })

  it('no note when the text was understood', async () => {
    await typeAndDeal('funny')
    await screen.findByRole('button', { name: "I'd watch it" })
    expect(screen.queryByText(/We went with your moods/)).toBeNull()
  })
})
