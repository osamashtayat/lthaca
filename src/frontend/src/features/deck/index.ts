/** Public surface of the deck feature: the solo deck pages, and the reusable swipe card + gesture for the group deck (FE-11). */
export { DeckStartPage } from './pages/DeckStartPage'
export { DeckPage } from './pages/DeckPage'
export { DeckTopPage } from './pages/DeckTopPage'
export { SwipeCard } from './components/SwipeCard'
export { useSwipeGesture } from './useSwipeGesture'
export { directionFromDrag, SWIPE_THRESHOLD } from './swipeGesture'
export type { SwipeDirection, SeenVerdict } from './types'
