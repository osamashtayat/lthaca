// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { PrivacyPage } from './PrivacyPage'
import { TermsPage } from './TermsPage'

afterEach(cleanup)

describe('legal pages (L5 texts)', () => {
  it('Terms: eight points, TMDB sentence, placeholders visible', () => {
    render(<TermsPage />)
    expect(screen.getByRole('heading', { level: 1, name: 'Terms of Use' })).toBeTruthy()
    expect(screen.getAllByRole('heading', { level: 2 })).toHaveLength(8)
    expect(screen.getByText(/not endorsed or certified by TMDB/)).toBeTruthy()
    expect(screen.getByText(/\[your name or company\]/)).toBeTruthy()
    expect(screen.getByText('Last updated: [date]')).toBeTruthy()
  })

  it('Privacy: seven points, three services, placeholders visible', () => {
    render(<PrivacyPage />)
    expect(screen.getByRole('heading', { level: 1, name: 'Privacy Policy' })).toBeTruthy()
    expect(screen.getAllByRole('heading', { level: 2 })).toHaveLength(7)
    expect(screen.getAllByRole('listitem').filter((li) => li.closest('ul'))).toHaveLength(3)
    expect(screen.getByText(/\[country\]/)).toBeTruthy()
  })
})
