// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Button } from './Button'
import { IconButton } from './IconButton'
import { SegmentedControl } from './SegmentedControl'
import { ToggleChip } from './Chip'

afterEach(cleanup)

describe('Button', () => {
  it.each([
    ['ink', 'bg-ink'],
    ['yellow', 'bg-yellow'],
    ['pink', 'bg-pink'],
    ['outline', 'bg-transparent'],
  ] as const)('variant %s', (variant, cls) => {
    render(<Button variant={variant}>Go</Button>)
    expect(screen.getByRole('button', { name: 'Go' }).className).toContain(cls)
  })

  it('defaults to type=button and fires onClick', async () => {
    const onClick = vi.fn<() => void>()
    render(<Button onClick={onClick}>Go</Button>)
    const button = screen.getByRole('button', { name: 'Go' })
    expect(button.getAttribute('type')).toBe('button')
    await userEvent.click(button)
    expect(onClick).toHaveBeenCalled()
  })
})

describe('IconButton', () => {
  it('is named by its aria-label', () => {
    render(<IconButton aria-label="Close">x</IconButton>)
    expect(screen.getByRole('button', { name: 'Close' })).toBeTruthy()
  })
})

describe('ToggleChip', () => {
  it('reflects selected in aria-pressed', () => {
    render(<ToggleChip selected>Drama</ToggleChip>)
    expect(screen.getByRole('button', { name: 'Drama' }).getAttribute('aria-pressed')).toBe('true')
  })
})

describe('SegmentedControl', () => {
  it('arrow keys select the next option', async () => {
    const onChange = vi.fn<(value: string) => void>()
    render(
      <SegmentedControl
        label="Who"
        options={[
          { value: 'a', label: 'A' },
          { value: 'b', label: 'B' },
        ]}
        value="a"
        onChange={onChange}
      />,
    )
    await userEvent.tab()
    await userEvent.keyboard('{ArrowRight}')
    expect(onChange).toHaveBeenCalledWith('b')
  })
})

describe('ButtonLink', () => {
  it('is a link with the button look', async () => {
    const { MemoryRouter } = await import('react-router')
    const { ButtonLink } = await import('./Button')
    render(
      <MemoryRouter>
        <ButtonLink to="/deck" variant="yellow">
          Go
        </ButtonLink>
      </MemoryRouter>,
    )
    const link = screen.getByRole('link', { name: 'Go' })
    expect(link.getAttribute('href')).toBe('/deck')
    expect(link.className).toContain('bg-yellow')
  })
})
