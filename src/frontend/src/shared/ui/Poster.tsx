import { useState } from 'react'

type PosterProps = {
  /** Full TMDB image URL (built by the caller from the API's poster path), or null. */
  src: string | null | undefined
  alt: string
  className?: string
}

/** 2:3 film poster; a beige placeholder when there is no image or it fails to load. */
export function Poster({ src, alt, className = '' }: PosterProps) {
  const [failedSrc, setFailedSrc] = useState<string | null>(null)
  const showImage = !!src && src !== failedSrc

  return (
    <div className={`aspect-[2/3] overflow-hidden rounded-tile border-2 border-ink bg-beige ${className}`}>
      {showImage ? (
        <img src={src} alt={alt} loading="lazy" onError={() => setFailedSrc(src)} className="size-full object-cover" />
      ) : (
        <div className="flex size-full items-center justify-center p-2 text-center">
          <span className="sr-only">{alt}</span>
          <span aria-hidden="true" className="font-mono text-xs uppercase tracking-[0.12em] text-ink-muted">
            No poster
          </span>
        </div>
      )}
    </div>
  )
}
