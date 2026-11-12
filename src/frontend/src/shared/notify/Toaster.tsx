import { Toaster as SonnerToaster } from 'sonner'

/** Mounted once by AppShell. Top-center: on phones the bottom edge belongs to the deck gestures. */
export function Toaster() {
  return (
    <SonnerToaster
      position="top-center"
      closeButton
      toastOptions={{
        style: {
          fontFamily: 'var(--font-sans)',
          background: 'var(--color-cream)',
          color: 'var(--color-ink)',
          border: 'var(--border-ink) solid var(--color-ink)',
          borderRadius: 'var(--radius-control)',
          boxShadow: 'var(--shadow-hard)',
        },
      }}
    />
  )
}
