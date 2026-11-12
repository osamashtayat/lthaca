import { Link, NavLink } from 'react-router'
import { MenuIcon, Wordmark } from '@/shared/ui'
import { NAV_ITEMS } from './navigation'

/**
 * Top header (never a bottom tab bar). Phones: wordmark · ☰ (no bell until notifications exist, ADR-0014). From `lg` the main items become pills.
 * Structure only — visual polish follows the design canvas in the screens' own tasks.
 */
export function AppHeader({ menuOpen, onOpenMenu }: { menuOpen: boolean; onOpenMenu: () => void }) {
  return (
    <header className="sticky top-0 z-20 border-b-2 border-ink bg-cream">
      <div className="mx-auto flex h-(--header-height) max-w-(--content-max-width) items-center gap-2.5 pr-4 pl-5 lg:h-(--header-height-lg) lg:gap-7 lg:px-8">
        <Link to="/" className="text-[26px] text-ink no-underline lg:text-[30px]">
          <Wordmark />
        </Link>

        <nav aria-label="Main" className="hidden flex-1 gap-1.5 lg:flex">
          {NAV_ITEMS.filter((item) => item.inHeader).map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className="flex h-10 items-center rounded-full border-2 border-transparent px-4 font-bold text-ink no-underline aria-[current=page]:border-ink aria-[current=page]:bg-yellow"
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-2.5">
          <button
            type="button"
            onClick={onOpenMenu}
            aria-label="Open menu"
            aria-expanded={menuOpen}
            aria-controls="main-menu"
            className="flex size-11 items-center justify-center rounded-control bg-ink text-cream lg:hidden"
          >
            <MenuIcon />
          </button>
        </div>
      </div>
    </header>
  )
}
