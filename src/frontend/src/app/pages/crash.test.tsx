// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CrashBoundary } from '@/shared/monitoring'
import { CrashPage } from './CrashPage'
import { RouteErrorPage } from './RouteErrorPage'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

function Boom(): never {
  throw new Error('render failed')
}

function renderRoute(element: React.ReactNode | null, loader?: () => never) {
  const router = createMemoryRouter([{ path: '/', element, loader, errorElement: <RouteErrorPage /> }], { initialEntries: ['/'] })
  render(<RouterProvider router={router} />)
}

describe('crash screens (FE-21)', () => {
  it('shows the crash screen with a reload action when a route fails to render', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    renderRoute(<Boom />)
    expect(await screen.findByRole('heading', { name: "Well, that's a plot twist" })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Reload' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Back to tonight' }).getAttribute('href')).toBe('/')
  })

  it('shows "not found" for a thrown 404, not a crash', async () => {
    renderRoute(<p>never</p>, () => {
      throw new Response('missing', { status: 404 })
    })
    expect(await screen.findByRole('heading', { name: 'Lost the plot' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Reload' })).toBeNull()
  })

  it('shows the crash screen when a route fails to load', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    renderRoute(<p>never</p>, () => {
      throw new Error('chunk failed')
    })
    expect(await screen.findByRole('heading', { name: "Well, that's a plot twist" })).toBeTruthy()
  })

  it('shows the crash screen for an error outside the router', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    render(
      <CrashBoundary fallback={<CrashPage />}>
        <Boom />
      </CrashBoundary>,
    )
    expect(screen.getByRole('heading', { name: "Well, that's a plot twist" })).toBeTruthy()
  })
})
