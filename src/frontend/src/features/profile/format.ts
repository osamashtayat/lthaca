const month = new Intl.DateTimeFormat('en-US', { month: 'short', year: 'numeric', timeZone: 'UTC' })
const day = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' })

/** "Nov 2026" for "since Nov 2026". UTC, so the label doesn't depend on the viewer's time zone. */
export const formatSince = (iso: string) => month.format(new Date(iso))

/** "13 Nov 2026"-style date, UTC. */
export const formatDate = (iso: string) => day.format(new Date(iso))

/** The first letter of the name for the avatar disc. */
export const initialOf = (name: string) => (name.trim().charAt(0) || '?').toUpperCase()
