// @vitest-environment jsdom
import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockFilms } from '@/mocks/fixtures'
import { SwipeCard } from './SwipeCard'

afterEach(cleanup)

const film = mockFilms[0]!
const longStory = `${'A very long story about a club that meets in a basement. '.repeat(12)}The end.`

describe('SwipeCard story (FE-22)', () => {
  it('shows the facts, the title and the start of the story', () => {
    render(<SwipeCard film={{ ...film, overview: longStory }} onSwipe={() => {}} />)

    const card = screen.getByRole('article')
    expect(within(card).getByText(longStory).className).toContain('line-clamp-3')
    expect(within(card).getByText(/1999 · 139 min/)).toBeTruthy()
    expect(within(card).getByText(/★ \d\.\d/)).toBeTruthy()
  })

  it('shows no story text when the film has none', () => {
    render(<SwipeCard film={{ ...film, overview: null }} onSwipe={() => {}} />)

    expect(screen.getByRole('article').querySelector('p.line-clamp-3')).toBeNull()
  })

  it('"More" opens the whole story in a sheet and never swipes', async () => {
    const onSwipe = vi.fn<(direction: string) => void>()
    const user = userEvent.setup()
    render(<SwipeCard film={{ ...film, overview: longStory }} onSwipe={onSwipe} />)

    await user.click(screen.getByRole('button', { name: `The story of ${film.title}` }))

    const sheet = await screen.findByRole('dialog', { name: film.title })
    expect(within(sheet).getByText(longStory)).toBeTruthy()
    await user.keyboard('{ArrowRight}')
    await user.click(within(sheet).getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(onSwipe).not.toHaveBeenCalled()
  })

  it('the sheet says so when there is no story', async () => {
    const user = userEvent.setup()
    render(<SwipeCard film={{ ...film, overview: null }} onSwipe={() => {}} />)

    await user.click(screen.getByRole('button', { name: `The story of ${film.title}` }))

    expect(within(await screen.findByRole('dialog')).getByText('No story for this one yet.')).toBeTruthy()
  })
})
