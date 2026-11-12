import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { AppProviders } from '@/app/providers'
import { router } from '@/app/router'
import { API_MOCKS_ENABLED } from '@/shared/api'
import '@/shared/styles/global.css'

const rootElement = document.getElementById('root')
if (!rootElement) throw new Error('Root element #root not found')
const root = createRoot(rootElement)

/**
 * On the dev server the API is mocked (MSW, ADR-0020) so frontend work never waits for the backend; set
 * VITE_API_MOCKS=off to use the real API. The dynamic import keeps the mocks out of production builds
 * (scripts/check-no-mocks.mjs fails the build otherwise). Resolves to false only when mocking was wanted but failed.
 */
async function enableMocking(): Promise<boolean> {
  // The DEV check repeats what API_MOCKS_ENABLED already says; it is here so the build can drop the import statically.
  if (!import.meta.env.DEV || !API_MOCKS_ENABLED) return true
  try {
    const { startMockApi } = await import('@/mocks/browser')
    await startMockApi()
    return true
  } catch (error) {
    // e.g. a browser that blocks service workers: show the app anyway (requests then reach the real API).
    console.error('Could not start the mock API; continuing without it.', error)
    return false
  }
}

void enableMocking().then(async (mocksOk) => {
  // The DEV check (not just mocksOk) lets the production build drop the banner chunk entirely.
  const MockApiFailedBanner =
    import.meta.env.DEV && !mocksOk ? (await import('@/mocks/MockApiFailedBanner')).MockApiFailedBanner : null
  root.render(
    <StrictMode>
      {MockApiFailedBanner && <MockApiFailedBanner />}
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>
    </StrictMode>,
  )
})
