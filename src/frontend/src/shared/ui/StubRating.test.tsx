// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import { StubRating } from './StubRating'

afterEach(cleanup)

function Harness({ initial = null }: { initial?: number | null }) {
  const [value, setValue] = useState<number | null>(initial)
  return <StubRating label="Your rating" value={value} onChange={setValue} />
}

describe('StubRating', () => {
  it('is a radio group of five stubs with the current value checked', () => {
    render(<Harness initial={3} />)
    expect(screen.getByRole('radiogroup', { name: 'Your rating' })).toBeTruthy()
    expect(screen.getAllByRole('radio')).toHaveLength(5)
    expect((screen.getByRole('radio', { name: '3 of 5' }) as HTMLInputElement).checked).toBe(true)
  })

  it('has one tab stop: the first stub when unrated', async () => {
    render(<Harness />)
    await userEvent.tab()
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: '1 of 5' }))
  })

  it('arrow keys move focus and select', async () => {
    render(<Harness initial={2} />)
    await userEvent.tab()
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: '2 of 5' }))
    await userEvent.keyboard('{ArrowRight}')
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: '3 of 5' }))
    expect((screen.getByRole('radio', { name: '3 of 5' }) as HTMLInputElement).checked).toBe(true)
    await userEvent.keyboard('{ArrowRight}{ArrowRight}')
    expect((screen.getByRole('radio', { name: '5 of 5' }) as HTMLInputElement).checked).toBe(true)
    await userEvent.keyboard('{ArrowRight}') // wraps
    expect((screen.getByRole('radio', { name: '1 of 5' }) as HTMLInputElement).checked).toBe(true)
  })

  it('selects on click', async () => {
    render(<Harness />)
    await userEvent.click(screen.getByRole('radio', { name: '4 of 5' }))
    expect((screen.getByRole('radio', { name: '4 of 5' }) as HTMLInputElement).checked).toBe(true)
  })
})
