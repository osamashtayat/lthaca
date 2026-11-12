/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** `off` → the dev server talks to the real API instead of the mock API (ADR-0020). Any other value: mocks on. */
  readonly VITE_API_MOCKS?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
