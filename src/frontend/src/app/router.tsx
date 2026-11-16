import { routes } from '@/app/routes'
import { createTracedBrowserRouter } from '@/shared/monitoring'

/** The app's router; traced so Sentry names page loads and navigations by route (ADR-0028). */
export const router = createTracedBrowserRouter(routes)
