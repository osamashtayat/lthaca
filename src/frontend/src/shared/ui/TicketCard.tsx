import type { ReactNode } from 'react'

type TicketCardProps = {
  children: ReactNode
  /** Stub text, left of the numeral. Defaults to the canvas' "ADMIT 1". */
  stubLabel?: string
  /** Big Shrikhand numeral on the stub (e.g. "1", or a room code). */
  stubValue?: string
  className?: string
}

function Notch({ side }: { side: 'left' | 'right' }) {
  const pos = side === 'left' ? '-left-3' : '-right-3'
  return (
    <span aria-hidden="true" className={`absolute -top-3 ${pos} size-6 rounded-full border-2 border-ink bg-cream`} />
  )
}

/** Ticket-style card: body, a dashed tear line with notches on both edges, and the "ADMIT 1" stub. */
export function TicketCard({ children, stubLabel = 'ADMIT 1', stubValue, className = '' }: TicketCardProps) {
  return (
    <article className={`overflow-hidden rounded-card border-[2.5px] border-ink bg-cream shadow-hard ${className}`}>
      <div className="p-5">{children}</div>
      <div className="relative border-t-2 border-dashed border-ink">
        <Notch side="left" />
        <Notch side="right" />
        <div className="flex items-center justify-between px-5 py-3">
          <span className="font-mono text-xs uppercase tracking-[0.12em] text-ink-muted">{stubLabel}</span>
          {stubValue && <span className="font-display text-2xl leading-none text-pink-text">{stubValue}</span>}
        </div>
      </div>
    </article>
  )
}
