import { initErrorTracking } from './errorTracking'

// Imported first by main.tsx, so Sentry is running before the router is created and before any other module throws.
initErrorTracking()
