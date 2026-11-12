import { useCallback, useState } from 'react'
import { Outlet } from 'react-router'
import { Toaster } from '@/shared/notify'
import { AppHeader } from './AppHeader'
import { MenuSheet } from './MenuSheet'

/** Page frame for every screen: header, the ☰ menu, the page outlet and the toaster. Mobile first. */
export function AppShell() {
  const [menuOpen, setMenuOpen] = useState(false)
  const closeMenu = useCallback(() => setMenuOpen(false), [])

  return (
    <>
      <AppHeader menuOpen={menuOpen} onOpenMenu={() => setMenuOpen(true)} />
      {menuOpen && <MenuSheet onClose={closeMenu} />}
      <main className="mx-auto w-full max-w-(--content-max-width) px-5 py-6 lg:px-8">
        <Outlet />
      </main>
      <Toaster />
    </>
  )
}
