/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** `off` → the dev server talks to the real API instead of the mock API (ADR-0020). Any other value: mocks on. */
  readonly VITE_API_MOCKS?: string
  /** Sentry DSN of the ithaca-frontend project (ADR-0028). Empty or unset: error tracking off. Read at build time. */
  readonly VITE_SENTRY_DSN?: string
  /** Release name shown on Sentry issues; the deploy sets it (OPS-07). */
  readonly VITE_SENTRY_RELEASE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
