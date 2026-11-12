import { useState } from 'react'
import type { ReactNode } from 'react'
import {
  Button,
  Chip,
  CloseIcon,
  IconButton,
  MonoLabel,
  Poster,
  SegmentedControl,
  Sheet,
  StubRating,
  TicketCard,
  ToggleChip,
  type ButtonVariant,
} from '@/shared/ui'

const VARIANTS: ButtonVariant[] = ['ink', 'yellow', 'pink', 'outline']
const MODES = [
  { value: 'solo', label: 'Just me' },
  { value: 'group', label: 'With friends' },
] as const
const GENRES = ['Drama', 'Comedy', 'Sci-fi', 'Thriller']

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3">
      <MonoLabel>{title}</MonoLabel>
      {children}
    </section>
  )
}

/** Dev-only gallery of the shared UI kit (/dev/ui). View it at 375px. */
export function UiGalleryPage() {
  const [mode, setMode] = useState<(typeof MODES)[number]['value']>('solo')
  const [rating, setRating] = useState<number | null>(3)
  const [genres, setGenres] = useState<string[]>(['Drama'])
  const [sheetOpen, setSheetOpen] = useState(false)

  return (
    <div className="flex flex-col gap-8">
      <h1 className="text-3xl font-extrabold tracking-tight">UI kit</h1>

      <Section title="Button">
        <div className="flex flex-wrap gap-3">
          {VARIANTS.map((v) => (
            <Button key={v} variant={v}>
              {v}
            </Button>
          ))}
        </div>
        <Button size="lg" className="w-full">
          Start swiping
        </Button>
      </Section>

      <Section title="IconButton">
        <IconButton aria-label="Close">
          <CloseIcon />
        </IconButton>
      </Section>

      <Section title="Chip / ToggleChip">
        <div className="flex flex-wrap gap-2">
          <Chip>1999</Chip>
          <Chip>139 min</Chip>
        </div>
        <div className="flex flex-wrap gap-2">
          {GENRES.map((g) => (
            <ToggleChip
              key={g}
              selected={genres.includes(g)}
              onClick={() => setGenres((cur) => (cur.includes(g) ? cur.filter((x) => x !== g) : [...cur, g]))}
            >
              {g}
            </ToggleChip>
          ))}
        </div>
      </Section>

      <Section title="SegmentedControl">
        <SegmentedControl label="Who is watching" options={MODES} value={mode} onChange={setMode} />
      </Section>

      <Section title="StubRating">
        <StubRating label="Your rating" value={rating} onChange={setRating} />
      </Section>

      <Section title="TicketCard">
        <TicketCard stubValue="7">
          <h2 className="text-2xl font-extrabold tracking-tight">By mood</h2>
          <p className="mt-1 text-ink-muted">Tell us how tonight feels.</p>
        </TicketCard>
      </Section>

      <Section title="Poster">
        <div className="flex gap-3">
          <Poster src={null} alt="Poster not available" className="w-28" />
          <Poster src="/missing-poster.jpg" alt="Poster that fails to load" className="w-28" />
        </div>
      </Section>

      <Section title="Sheet">
        <Button variant="yellow" onClick={() => setSheetOpen(true)}>
          Open sheet
        </Button>
        <Sheet open={sheetOpen} onClose={() => setSheetOpen(false)} title="Filters">
          <p>Sheet content goes here.</p>
        </Sheet>
      </Section>
    </div>
  )
}
