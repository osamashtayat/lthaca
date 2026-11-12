import type { RouteObject } from 'react-router'
import { AppShell } from '@/app/layout/AppShell'
import { PlaceholderPage } from '@/app/PlaceholderPage'
import { DevStatusPage } from '@/app/dev/DevStatusPage'
import { NotFoundPage } from '@/app/pages/NotFoundPage'
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
      { path: '/deck', element: <PlaceholderPage title="The deck" feature="deck" /> },
      { path: '/mood', element: <PlaceholderPage title="By mood" feature="mood" /> },
      { path: '/taste', element: <PlaceholderPage title="By taste" feature="taste" /> },
      { path: '/rooms', element: <PlaceholderPage title="Movie nights" feature="rooms" /> },
      { path: '/rooms/:code', element: <PlaceholderPage title="Room lobby" feature="rooms" /> },
      { path: '/rooms/:code/swipe', element: <PlaceholderPage title="Group swipe" feature="rooms" /> },
      { path: '/rooms/:code/result', element: <PlaceholderPage title="It's a match" feature="rooms" /> },
      { path: '/cinema', element: <PlaceholderPage title="My Cinema" feature="library" /> },
      { path: '/browse', element: <PlaceholderPage title="Browse" feature="browse" /> },
      { path: '/import', element: <PlaceholderPage title="Import from Letterboxd" feature="import" /> },
      { path: '/profile', element: <PlaceholderPage title="Profile & settings" feature="profile" /> },
      { path: '/updates', element: <PlaceholderPage title="Updates" feature="(open question: notifications)" /> },
      { path: '/sign-in', element: <PlaceholderPage title="Welcome" feature="auth" /> },
      // Reference slice: the one real page.
      { path: '/films/:tmdbId', element: <FilmPage /> },
      { path: '/dev/status', element: <DevStatusPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]
