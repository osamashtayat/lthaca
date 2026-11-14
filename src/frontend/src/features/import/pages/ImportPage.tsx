import { useState } from 'react'
import { Button, ErrorState, LoadingState } from '@/shared/ui'
import { useImport } from '../api/useImport'
import { useLatestImport } from '../api/useLatestImport'
import { ImportPicker } from '../components/ImportPicker'
import { ImportResult, ImportRunning } from '../components/ImportProgress'
import type { ImportJob } from '../types'

/**
 * `/import` (user-only): the intro and file picker; once an import exists, its progress, then its result. A reload
 * resumes the newest import, so progress is never lost. What the import does is the server's: this screen shows it.
 */
export function ImportPage() {
  const latest = useLatestImport()
  const [created, setCreated] = useState<ImportJob | null>(null)
  const [again, setAgain] = useState(false)
  const activeId = created?.id ?? latest.data?.id ?? null
  const job = useImport(activeId, created ?? latest.data ?? undefined)

  if (latest.isPending) return <LoadingState label="Loading…" />
  if (latest.isError) return <ErrorState error={latest.error} onRetry={() => void latest.refetch()} />

  const current = activeId ? job.data : undefined
  const running = current?.status === 'queued' || current?.status === 'running'

  return (
    <section className="mx-auto flex w-full max-w-md flex-col gap-6 pb-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-4xl font-extrabold tracking-tight">Import from Letterboxd</h1>
        <p className="text-ink-muted">Bring your ratings, likes and watchlist into My Cinema. Ithaca&apos;s own data is never removed.</p>
      </div>

      {job.isError && !current && <ErrorState error={job.error} onRetry={() => void job.refetch()} />}

      {current && running && <ImportRunning job={current} />}

      {current?.status === 'completed' && !again && (
        <>
          <ImportResult job={current} />
          <Button variant="outline" onClick={() => setAgain(true)} className="self-start">
            Import a newer export
          </Button>
        </>
      )}

      {current?.status === 'failed' && !again && (
        <p role="alert" className="font-bold text-pink-text">
          That import didn&apos;t finish. Nothing already added was lost: try it again.
        </p>
      )}

      {!running && (!current || again || current.status === 'failed') && (
        <ImportPicker
          onCreated={(next) => {
            setCreated(next)
            setAgain(false)
          }}
        />
      )}
    </section>
  )
}
