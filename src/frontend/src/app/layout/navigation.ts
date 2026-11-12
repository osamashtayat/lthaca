/**
 * Main navigation, defined once: the desktop header pills and the mobile ☰ menu both render this list.
 * `meta` is the small DM Mono label shown in the menu.
 */
export interface NavItem {
  to: string
  label: string
  meta: string
  /** Shown as a pill in the desktop header (≥ lg). Everything is in the ☰ menu. */
  inHeader: boolean
}

export const NAV_ITEMS: readonly NavItem[] = [
  { to: '/', label: 'Tonight', meta: 'PICK A FILM', inHeader: true },
  { to: '/browse', label: 'Browse', meta: 'SEARCH', inHeader: true },
  { to: '/cinema', label: 'My Cinema', meta: 'SAVED', inHeader: true },
  { to: '/updates', label: 'Updates', meta: 'NEWS', inHeader: false },
  { to: '/rooms', label: 'Movie nights', meta: 'ROOMS', inHeader: false },
]
