// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { AppFooter } from './AppFooter'

afterEach(cleanup)

describe('AppFooter (TMDB attribution, ADR-0017)', () => {
  it('shows the TMDB logo, small', () => {
    render(<AppFooter />, { wrapper: MemoryRouter })
    const logo = screen.getByRole('img', { name: 'TMDB' })
    expect(logo.className).toContain('h-3')
  })

  it('shows the required notice', () => {
    render(<AppFooter />, { wrapper: MemoryRouter })
    expect(screen.getByText(/uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB/)).toBeTruthy()
  })

  it('links to Terms and Privacy', () => {
    render(<AppFooter />, { wrapper: MemoryRouter })
    expect(screen.getByRole('link', { name: 'Terms' }).getAttribute('href')).toBe('/terms')
    expect(screen.getByRole('link', { name: 'Privacy' }).getAttribute('href')).toBe('/privacy')
  })
})
