import { ErrorState, LoadingState, Sheet } from '@/shared/ui'
import { useTrailer } from '../api/useTrailer'

/** The trailer in a bottom sheet. Fetches only once opened; YouTube's privacy-enhanced domain (no cookies until play). */
export function TrailerSheet({ tmdbId, open, onClose }: { tmdbId: number; open: boolean; onClose: () => void }) {
  const trailer = useTrailer(tmdbId, open)

  return (
    <Sheet open={open} onClose={onClose} title="Trailer">
      {trailer.isPending && <LoadingState label="Loading trailer…" />}
      {trailer.isError && <ErrorState error={trailer.error} onRetry={() => void trailer.refetch()} />}
      {trailer.isSuccess && trailer.data === null && <p>No trailer for this film yet.</p>}
      {trailer.isSuccess && trailer.data !== null && (
        <div className="aspect-video overflow-hidden rounded-tile border-2 border-ink bg-ink">
          <iframe
            src={`https://www.youtube-nocookie.com/embed/${encodeURIComponent(trailer.data.key)}`}
            title={trailer.data.name}
            allow="encrypted-media; picture-in-picture; fullscreen"
            allowFullScreen
            referrerPolicy="strict-origin-when-cross-origin"
            className="size-full"
          />
        </div>
      )}
    </Sheet>
  )
}
