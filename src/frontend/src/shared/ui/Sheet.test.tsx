// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Sheet } from './Sheet'

afterEach(cleanup)

describe('Sheet', () => {
  it('opens the native dialog when open and names it by its title', () => {
    render(
      <Sheet open onClose={() => {}} title="Filters">
        <button type="button">Inside</button>
      </Sheet>,
    )
    expect(screen.getByRole('dialog', { name: 'Filters' })).toBeTruthy()
  })

  it('stays closed while open is false', () => {
    render(
      <Sheet open={false} onClose={() => {}} title="Filters">
        body
      </Sheet>,
    )
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('calls onClose from the close button and from the backdrop', async () => {
    const onClose = vi.fn<() => void>()
    render(
      <Sheet open onClose={onClose} title="Filters">
        body
      </Sheet>,
    )
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(onClose).toHaveBeenCalledTimes(1)
    await userEvent.click(screen.getByRole('dialog', { name: 'Filters' }))
    expect(onClose).toHaveBeenCalledTimes(2)
  })

  it('closes the dialog when open turns false', () => {
    const { rerender } = render(
      <Sheet open onClose={() => {}} title="Filters">
        body
      </Sheet>,
    )
    rerender(
      <Sheet open={false} onClose={() => {}} title="Filters">
        body
      </Sheet>,
    )
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('calls onClose exactly once when the parent closes it', () => {
    const onClose = vi.fn<() => void>()
    const { rerender } = render(
      <Sheet open onClose={onClose} title="Filters">
        body
      </Sheet>,
    )
    rerender(
      <Sheet open={false} onClose={onClose} title="Filters">
        body
      </Sheet>,
    )
    expect(onClose).not.toHaveBeenCalled()
  })
})
