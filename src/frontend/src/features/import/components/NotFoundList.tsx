import { notify } from '@/shared/notify'
import { Button, ErrorState, LoadingState, MonoLabel } from '@/shared/ui'
import { useImportNotFound } from '../api/useImportNotFound'
import type { ImportNotFound } from '../types'

const REASON_TEXT: Record<ImportNotFound['reason'], string> = {
  noMatch: "We couldn't find this film in our catalog.",
  lookupFailed: "We couldn't look this one up. Import again later to retry.",
}

/** Only a real https link goes into an `href`: the URI comes from the file the user uploaded. */
const safeLink = (uri: string | null) => (uri !== null && uri.startsWith('https://') ? uri : null)

/** "Not found": title · year, a link to the Letterboxd page, one line per reason, paged with "Load more". */
export function NotFoundList({ importId, listKept }: { importId: string; listKept: boolean }) {
  const list = useImportNotFound(importId, listKept)

  if (!listKept) {
    return <p className="text-ink-muted">The list of films we couldn&apos;t find is only kept for your latest import.</p>
  }
  if (list.isPending) return <LoadingState label="Loading the list…" />
  if (list.isError) return <ErrorState error={list.error} onRetry={() => void list.refetch()} />
  const items = list.data.pages.flatMap((p) => p.items)

  return (
    <div className="flex flex-col gap-3">
      <MonoLabel>Not found · {list.data.pages[0]?.totalCount ?? 0}</MonoLabel>
      <ul className="flex flex-col gap-2">
        {items.map((item, i) => {
          const link = safeLink(item.letterboxdUri)
          return (
            <li key={`${item.title}-${item.year ?? ''}-${i}`} className="flex flex-col gap-0.5 rounded-control border-2 border-ink bg-cream px-4 py-2">
              <span className="font-bold">
                {link ? (
                  <a href={link} target="_blank" rel="noopener noreferrer" className="inline-flex min-h-11 items-center text-pink-text">
                    {item.title}
                    {item.year !== null ? ` · ${item.year}` : ''}
                  </a>
                ) : (
                  <>
                    {item.title}
                    {item.year !== null ? ` · ${item.year}` : ''}
                  </>
                )}
              </span>
              <span className="text-sm text-ink-muted">{REASON_TEXT[item.reason]}</span>
            </li>
          )
        })}
      </ul>
      {list.hasNextPage && (
        <Button variant="outline" disabled={list.isFetchingNextPage} onClick={() => void list.fetchNextPage().then((r) => r.isError && notify.apiError(r.error))} className="self-start">
          {list.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </Button>
      )}
    </div>
  )
}
