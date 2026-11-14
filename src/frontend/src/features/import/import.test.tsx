// @vitest-environment jsdom
import { QueryClientProvider } from '@tanstack/react-query'
import { act, cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
// @ts-expect-error node types are not part of the frontend tsconfig; this is a test-only Node import
import { File as NodeFile } from 'node:buffer'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { importAlreadyRunning } from '@/mocks/overrides'
import { scriptMockImport } from '@/mocks/importStore'
import { server } from '@/mocks/server'
import { createQueryClient, queryKeys } from '@/shared/query'
import { ImportPage } from './pages/ImportPage'

vi.mock('@/shared/notify', () => ({ notify: { apiError: vi.fn<(e?: unknown) => void>(), error: vi.fn<(m: string) => void>() } }))

// jsdom's FormData and File are not the ones Node's fetch understands: use Node's own so the upload body really is multipart.
beforeAll(async () => {
  const sample = await new Response('a=1', { headers: { 'content-type': 'application/x-www-form-urlencoded' } }).formData()
  vi.stubGlobal('FormData', sample.constructor)
})

afterEach(() => {
  cleanup()
  vi.useRealTimers()
  server.events.removeAllListeners()
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT'
})

// The app's own client config (30 s stale time): a job handed over by the upload is not refetched at once.
function renderImport(client = createQueryClient()) {
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/import']}>
        <Routes>
          <Route path="/import" element={<ImportPage />} />
          <Route path="/cinema" element={<p>my cinema</p>} />
          <Route path="/" element={<p>Tonight</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return client
}

const picker = () => screen.findByLabelText('Choose your Letterboxd ZIP')
const zip = (name = 'letterboxd-export.zip') => new NodeFile(['PK'], name, { type: 'application/zip' }) as unknown as File

describe('Import: upload', () => {
  it('sends the file as multipart with the XSRF header', async () => {
    document.cookie = 'XSRF-TOKEN=token-123'
    const seen: Request[] = []
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/api/imports')) seen.push(request.clone())
    })
    const user = userEvent.setup()
    renderImport()
    await user.upload(await picker(), zip())
    await screen.findByRole('heading', { name: /Waiting to start|Importing your films/ })
    expect(seen).toHaveLength(1)
    expect(seen[0]?.headers.get('x-xsrf-token')).toBe('token-123')
    expect(seen[0]?.headers.get('content-type')).toMatch(/^multipart\/form-data; boundary=/)
    expect(await seen[0]?.text()).toContain('name="file"; filename="letterboxd-export.zip"')
  })

  it.each([
    ['invalid.zip', /doesn't look like a Letterboxd export/],
    ['toolarge.zip', /too big to upload/],
    ['huge.zip', /too big to import in one go/],
    ['empty.zip', /no films in it yet/],
    ['many.zip', /more films than we can import at once/],
  ])('shows the text for %s next to the picker', async (name, text) => {
    const user = userEvent.setup()
    renderImport()
    await user.upload(await picker(), zip(name))
    expect((await screen.findByRole('alert')).textContent).toMatch(text)
    expect(screen.getByLabelText('Choose your Letterboxd ZIP')).toBeTruthy() // the user can pick another file
  })

  it('says an import is already running (409)', async () => {
    server.use(importAlreadyRunning())
    const user = userEvent.setup()
    renderImport()
    await user.upload(await picker(), zip())
    expect((await screen.findByRole('alert')).textContent).toMatch(/already running/)
  })
})

describe('Import: progress and result', () => {
  it('polls while running, shows "N of M films", and stops when it completes', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: (ms) => vi.advanceTimersByTime(ms) })
    const client = renderImport()
    const invalidate = vi.spyOn(client, 'invalidateQueries')
    await user.upload(await picker(), zip())
    expect(await screen.findByText('Waiting to start…')).toBeTruthy()
    await act(() => vi.advanceTimersByTimeAsync(2_100))
    expect(await screen.findByText('400 of 1,204 films')).toBeTruthy()
    await act(() => vi.advanceTimersByTimeAsync(2_100))
    expect(await screen.findByText('800 of 1,204 films')).toBeTruthy()
    await waitFor(
      async () => {
        await act(() => vi.advanceTimersByTimeAsync(2_100))
        expect(screen.getByRole('heading', { name: 'Done' })).toBeTruthy()
      },
      { timeout: 10_000 },
    )
    expect(screen.getByText(/1,159 new in My Cinema · 27 ratings you already had were kept/)).toBeTruthy()
    expect(screen.getByText('45 films not found')).toBeTruthy()
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.library.all }) // My Cinema refreshes after completion
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.taste.all })

    const reads: string[] = []
    server.events.on('request:start', ({ request }) => {
      if (/\/api\/imports\/[^/]+$/.test(request.url)) reads.push(request.url)
    })
    await act(() => vi.advanceTimersByTimeAsync(10_000))
    expect(reads).toHaveLength(0) // polling stopped
    expect(screen.getByRole('link', { name: 'Go to My Cinema' }).getAttribute('href')).toBe('/cinema')
  })

  it('resumes a running import after a reload', async () => {
    scriptMockImport({ status: 'running', processedCount: 312 })
    renderImport()
    expect(await screen.findByText('312 of 1,204 films')).toBeTruthy()
    expect(screen.queryByLabelText('Choose your Letterboxd ZIP')).toBeNull()
  })

  it('shows the result of a finished import and lets the user import a newer one', async () => {
    scriptMockImport({ status: 'completed', processedCount: 1204, matchedCount: 1159, notFoundCount: 0, changes: { newFilms: 418, seen: 418, rated: 300, liked: 80, watchlist: 12, ratingsKept: 1 } })
    const user = userEvent.setup()
    renderImport()
    expect(await screen.findByText('418 new in My Cinema · 1 rating you already had was kept')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Import a newer export' }))
    expect(await picker()).toBeTruthy()
  })

  it('tells the user a failed import can be retried', async () => {
    scriptMockImport({ status: 'failed' })
    renderImport()
    expect((await screen.findByRole('alert')).textContent).toMatch(/didn't finish/)
    expect(await picker()).toBeTruthy()
  })
})

describe('Import: not found', () => {
  const finished = (over = {}) =>
    scriptMockImport({ status: 'completed', processedCount: 1204, matchedCount: 1159, notFoundCount: 45, ...over })

  it('lists title · year with a Letterboxd link and a reason, and pages with Load more', async () => {
    finished()
    const user = userEvent.setup()
    renderImport()
    expect(await screen.findByText('Not found · 45')).toBeTruthy()
    expect(screen.getAllByRole('listitem')).toHaveLength(20)
    expect(screen.getAllByText("We couldn't find this film in our catalog.").length).toBeGreaterThan(0)
    const link = screen.getAllByRole('link', { name: /^Film \d+/ })[0]
    expect(link?.getAttribute('href')).toMatch(/^https:\/\/boxd\.it\//)
    expect(link?.getAttribute('rel')).toContain('noopener')
    await user.click(screen.getByRole('button', { name: 'Load more' }))
    await waitFor(() => expect(screen.getAllByRole('listitem')).toHaveLength(40))
    await user.click(screen.getByRole('button', { name: 'Load more' }))
    await waitFor(() => expect(screen.getAllByRole('listitem')).toHaveLength(45))
    expect(screen.queryByRole('button', { name: 'Load more' })).toBeNull()
  })

  it('explains an older import whose list was not kept', async () => {
    finished({ notFoundCount: 5, notFoundListKept: false })
    renderImport()
    expect(await screen.findByText(/only kept for your latest import/)).toBeTruthy()
  })
})

describe('Import: help', () => {
  it('opens the export steps from the "?" and closes them', async () => {
    const user = userEvent.setup()
    renderImport()
    const help = await screen.findByRole('button', { name: 'How to get your Letterboxd file' })
    await user.click(help)
    expect(await screen.findByRole('heading', { name: 'Get your Letterboxd file' })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Close' }))
    await waitFor(() => expect(document.activeElement).toBe(help))
  })
})
