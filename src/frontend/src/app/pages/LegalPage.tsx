import type { ReactNode } from 'react'

export type LegalItem = { title: string; body: ReactNode }

/**
 * Shared layout for the Terms and Privacy pages: a title, "Last updated" and a numbered list. The texts are the owner's
 * L5 texts (docs/backlog.md); the [placeholders] stay visible until the owner fills them before launch.
 */
export function LegalPage({ title, items }: { title: string; items: LegalItem[] }) {
  return (
    <article className="mx-auto flex w-full max-w-md flex-col gap-4 pb-6">
      <h1 className="text-4xl font-extrabold tracking-tight">{title}</h1>
      <p className="font-mono text-xs text-ink-muted">Last updated: [date]</p>
      <ol className="flex flex-col gap-4">
        {items.map((item, i) => (
          <li key={item.title} className="flex flex-col gap-1">
            <h2 className="font-bold">
              {i + 1}. {item.title}
            </h2>
            <div className="flex flex-col gap-1 text-ink-muted">{item.body}</div>
          </li>
        ))}
      </ol>
    </article>
  )
}
