/** Placeholder while a query loads. Announced politely to screen readers. */
export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <output className="block font-mono text-sm text-ink-muted">{label}</output>
  )
}
