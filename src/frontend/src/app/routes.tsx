import type { RouteObject } from 'react-router'
import { AppShell } from '@/app/layout/AppShell'
import { PlaceholderPage } from '@/app/PlaceholderPage'
import { NotFoundPage } from '@/app/pages/NotFoundPage'
import { RequireUser, SignInPage } from '@/features/auth'
import { DeckPage, DeckStartPage, DeckTopPage } from '@/features/deck'
import { MoodPage } from '@/features/mood'
import { BrowsePage } from '@/features/browse'
import { CinemaPage } from '@/features/library'
import { FilmPage } from '@/features/films'

/**
 * Route table. Paths follow the screens on the design canvas. `PlaceholderPage` marks screens a feature task
 * builds: replace the element with the feature's page (exported from features/<feature>/index.ts), keep the path.
 */
export const routes: RouteObject[] = [
  {
    element: <AppShell />,
    children: [
      { index: true, element: <PlaceholderPage title="Tonight" feature="tonight" /> },
      // User-only screens (api-contract: decks, library, profile need a signed-in User). Add new ones inside this group.
      {
        element: <RequireUser />,
        children: [
          { path: '/deck', element: <DeckStartPage /> },
          { path: '/deck/:deckId', element: <DeckPage /> },
          { path: '/deck/:deckId/top', element: <DeckTopPage /> },
          { path: '/mood', element: <MoodPage /> },
          { path: '/taste', element: <PlaceholderPage title="By taste" feature="taste" /> },
          { path: '/cinema', element: <CinemaPage /> },
          { path: '/import', element: <PlaceholderPage title="Import from Letterboxd" feature="import" /> },
          { path: '/profile', element: <PlaceholderPage title="Profile & settings" feature="profile" /> },
        ],
      },
      { path: '/rooms', element: <PlaceholderPage title="Movie nights" feature="rooms" /> },
      { path: '/rooms/:code', element: <PlaceholderPage title="Room lobby" feature="rooms" /> },
      { path: '/rooms/:code/swipe', element: <PlaceholderPage title="Group swipe" feature="rooms" /> },
      { path: '/rooms/:code/result', element: <PlaceholderPage title="It's a match" feature="rooms" /> },
      { path: '/browse', element: <BrowsePage /> },
      { path: '/sign-in', element: <SignInPage /> },
      // Reference slice: the one real page.
      { path: '/films/:tmdbId', element: <FilmPage /> },
      // Dev builds only: the import is dynamic, so production bundles don't contain the page at all.
      ...(import.meta.env.DEV
        ? [
            { path: '/dev/status', lazy: async () => ({ Component: (await import('@/app/dev/DevStatusPage')).DevStatusPage }) },
            { path: '/dev/ui', lazy: async () => ({ Component: (await import('@/app/dev/UiGalleryPage')).UiGalleryPage }) },
          ]
        : []),
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]
