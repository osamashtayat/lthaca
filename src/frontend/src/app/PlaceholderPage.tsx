/** Marks a screen a feature task will build. Replace the route's element with the feature's page; keep the path. */
export function PlaceholderPage({ title, feature }: { title: string; feature: string }) {
  return (
    <section className="flex flex-col gap-2">
      <p className="font-mono text-xs tracking-[0.12em] text-ink-muted">NOT BUILT YET · features/{feature}</p>
      <h1 className="text-3xl font-extrabold tracking-tight">{title}</h1>
    </section>
  )
}
