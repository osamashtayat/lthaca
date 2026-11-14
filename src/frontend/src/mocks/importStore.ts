import type { components } from '@/shared/api'
import { mockFilms } from './fixtures'
import { putMockUserFilm } from './libraryStore'

type ImportJobDto = components['schemas']['ImportJobDto']
type ImportNotFoundDto = components['schemas']['ImportNotFoundDto']

/** Mock file names pick an outcome (api-contract §13 error codes); anything else imports fine. */
const FILE_OUTCOMES = [
  ['invalid', { status: 400, code: 'import.invalid_file' }],
  ['toolarge', { status: 413, code: 'request.too_large' }],
  ['huge', { status: 413, code: 'import.file_too_large' }],
  ['empty', { status: 422, code: 'import.nothing_to_import' }],
  ['many', { status: 422, code: 'import.too_many_films' }],
] as const

export const MOCK_IMPORT_FILM_COUNT = 1204
export const MOCK_IMPORT_STEP = 400
export const MOCK_NOT_FOUND_COUNT = 45

type Stored = { job: ImportJobDto; kept: boolean }
const jobs: Stored[] = []

const NOW = '2026-11-13T12:00:00Z'

export function mockImportOutcome(fileName: string) {
  const name = fileName.toLowerCase()
  return FILE_OUTCOMES.find(([word]) => name.includes(word))?.[1]
}

export function hasActiveMockImport(): boolean {
  return jobs.some((j) => j.job.status === 'queued' || j.job.status === 'running')
}

export function createMockImport(): ImportJobDto {
  // The older import's not-found list is emptied (api-contract §13 rule 6).
  for (const j of jobs) {
    j.kept = false
    j.job = { ...j.job, notFoundListKept: false }
  }
  const job: ImportJobDto = {
    id: crypto.randomUUID(),
    status: 'queued',
    createdAt: NOW,
    finishedAt: null,
    filmCount: MOCK_IMPORT_FILM_COUNT,
    processedCount: 0,
    matchedCount: 0,
    addedToCatalogCount: 0,
    notFoundCount: 0,
    notFoundListKept: true,
    changes: { newFilms: 0, seen: 0, rated: 0, liked: 0, watchlist: 0, ratingsKept: 0 },
  }
  jobs.unshift({ job, kept: true })
  return job
}

/** Scripted outcome: each read of a running job advances it by `MOCK_IMPORT_STEP` films; the last step completes it. */
function advance(stored: Stored): ImportJobDto {
  const { job } = stored
  if (job.status === 'completed' || job.status === 'failed') return job
  const processed = Math.min(job.processedCount + MOCK_IMPORT_STEP, job.filmCount)
  const done = processed >= job.filmCount
  const matched = done ? job.filmCount - MOCK_NOT_FOUND_COUNT : Math.round(processed * 0.96)
  stored.job = {
    ...job,
    status: done ? 'completed' : 'running',
    finishedAt: done ? NOW : null,
    processedCount: processed,
    matchedCount: matched,
    notFoundCount: done ? MOCK_NOT_FOUND_COUNT : Math.round(processed * 0.04),
    changes: { newFilms: matched, seen: matched, rated: Math.round(matched * 0.6), liked: Math.round(matched * 0.2), watchlist: 12, ratingsKept: done ? 27 : 0 },
  }
  if (done) {
    // Mocks stay dumb: the first three catalog films become the user's, so My Cinema visibly changes.
    for (const film of mockFilms.slice(0, 3)) putMockUserFilm(film.tmdbId, { liked: true, onWatchlist: false, seen: true, rating: null })
  }
  return stored.job
}

export function listMockImports(): ImportJobDto[] {
  return jobs.map((j) => j.job)
}

/** A read of one job (this is what the client polls). */
export function readMockImport(id: string): ImportJobDto | undefined {
  const stored = jobs.find((j) => j.job.id === id)
  return stored ? advance(stored) : undefined
}

export function mockNotFound(id: string): ImportNotFoundDto[] | undefined {
  const stored = jobs.find((j) => j.job.id === id)
  if (!stored) return undefined
  if (!stored.kept) return []
  return Array.from({ length: stored.job.notFoundCount }, (_, i) => ({
    title: `Film ${String(i + 1).padStart(2, '0')}`,
    year: i % 5 === 0 ? null : 1990 + (i % 30),
    letterboxdUri: i % 7 === 0 ? null : `https://boxd.it/mock${i}`,
    reason: i % 9 === 0 ? ('lookupFailed' as const) : ('noMatch' as const),
  })).sort((a, b) => a.title.localeCompare(b.title))
}

/** Test helper: scripts a job straight to a state (e.g. `running` at 312 of 1,204) and returns it. */
export function scriptMockImport(patch: Partial<ImportJobDto>): ImportJobDto {
  const job = createMockImport()
  const stored = jobs[0]
  if (stored) stored.job = { ...job, ...patch }
  return stored?.job ?? job
}

/** Called from src/test/setup.ts after every test. */
export function resetMockImports() {
  jobs.length = 0
}
