import type { ReactNode } from 'react'
import { CheckIcon, CloseIcon, HeartIcon } from '@/shared/ui'
import type { SwipeDirection } from '../types'

function RoundButton({ label, primary = false, onClick, children }: { label: string; primary?: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={onClick}
      className={`flex size-16 items-center justify-center rounded-full border-2 border-ink text-ink shadow-hard ${primary ? 'bg-pink' : 'bg-cream'}`}
    >
      {children}
    </button>
  )
}

/** The three round buttons under a card: "Not for me", "Seen it", "I'd watch it". Shared by the solo and the group deck. */
export function SwipeButtons({ onSwipe }: { onSwipe: (direction: SwipeDirection) => void }) {
  return (
    <div className="flex items-center justify-center gap-5">
      <RoundButton label="Not for me" onClick={() => onSwipe('left')}>
        <CloseIcon />
      </RoundButton>
      <RoundButton label="Seen it" onClick={() => onSwipe('up')}>
        <CheckIcon />
      </RoundButton>
      <RoundButton label="I'd watch it" primary onClick={() => onSwipe('right')}>
        <HeartIcon />
      </RoundButton>
    </div>
  )
}
