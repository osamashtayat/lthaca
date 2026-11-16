import { filmFacts, type Film } from '@/features/films'
import { MonoLabel, Sheet } from '@/shared/ui'

/**
 * The whole story of the film on the card, without leaving the deck (FE-22). The overview is TMDB's, shown as is
 * (display only, ADR-0017). Closing it puts the card back, ready to swipe.
 */
export function FilmStorySheet({ film, open, onClose }: { film: Film; open: boolean; onClose: () => void }) {
  return (
    <Sheet open={open} onClose={onClose} title={film.title}>
      <div className="flex flex-col gap-3">
        <MonoLabel>{filmFacts(film, { genres: 2 })}</MonoLabel>
        <p className="text-base leading-relaxed">{film.overview ?? 'No story for this one yet.'}</p>
      </div>
    </Sheet>
  )
}
