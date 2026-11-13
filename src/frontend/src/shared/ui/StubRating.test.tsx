// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import { StubRating } from './StubRating'
import { nextRating } from './stubRatingValue'

afterEach(cleanup)

function Harness({ initial = null }: { initial?: number | null }) {
  const [value, setValue] = useState<number | null>(initial)
  return <StubRating label="Your rating" value={value} onChange={setValue} />
}

const radio = (v: number) => screen.getByRole('radio', { name: `${v} of 5 stubs` }) as HTMLInputElement
const stub = (n: number) => screen.getByText(String(n)) // the whole-stub tap target

describe('StubRating', () => {
  it('is a radio group of ten half steps with the current value checked and read as "3.5 of 5 stubs"', () => {
    render(<Harness initial={3.5} />)
    expect(screen.getByRole('radiogroup', { name: 'Your rating' })).toBeTruthy()
    expect(screen.getAllByRole('radio')).toHaveLength(10)
    expect(radio(3.5).checked).toBe(true)
    expect(radio(3).checked).toBe(false)
  })

  it('has one tab stop: the first step when unrated', async () => {
    render(<Harness />)
    await userEvent.tab()
    expect(document.activeElement).toBe(radio(0.5))
  })

  it('arrow keys move by half a stub, and wrap', async () => {
    render(<Harness initial={2} />)
    await userEvent.tab()
    expect(document.activeElement).toBe(radio(2))
    await userEvent.keyboard('{ArrowRight}')
    expect(radio(2.5).checked).toBe(true)
    await userEvent.keyboard('{ArrowLeft}{ArrowLeft}')
    expect(radio(1.5).checked).toBe(true)
  })

  it('Home and End jump to 0.5 and 5', async () => {
    render(<Harness initial={3} />)
    await userEvent.tab()
    await userEvent.keyboard('{End}')
    expect(radio(5).checked).toBe(true)
    expect(document.activeElement).toBe(radio(5))
    await userEvent.keyboard('{Home}')
    expect(radio(0.5).checked).toBe(true)
  })

  it('a tap on a stub sets it; tapping it again takes half off (3.5 is tap 4, tap 4)', async () => {
    render(<Harness />)
    await userEvent.click(stub(4))
    expect(radio(4).checked).toBe(true)
    await userEvent.click(stub(4))
    expect(radio(3.5).checked).toBe(true)
    await userEvent.click(stub(4))
    expect(radio(4).checked).toBe(true) // and back up
  })

  it('reaches any value in at most two taps on one stub, including 0.5', async () => {
    render(<Harness />)
    await userEvent.click(stub(1))
    await userEvent.click(stub(1))
    expect(radio(0.5).checked).toBe(true)
  })

  it('nextRating: n, or n - 0.5 when the value is already n', () => {
    expect(nextRating(null, 3)).toBe(3)
    expect(nextRating(2, 3)).toBe(3)
    expect(nextRating(3, 3)).toBe(2.5)
    expect(nextRating(2.5, 3)).toBe(3)
  })

  it('does nothing when disabled', async () => {
    function Disabled() {
      const [v, setV] = useState<number | null>(2)
      return <StubRating label="Your rating" value={v} onChange={setV} disabled />
    }
    render(<Disabled />)
    await userEvent.click(stub(4))
    expect(radio(2).checked).toBe(true)
  })
})
