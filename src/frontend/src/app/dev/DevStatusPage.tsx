import { Link } from 'react-router'
import { useRoomHub } from '@/shared/realtime'
import { useHealth } from './api/useHealth'

/**
 * /dev/status — plumbing check, not a product screen: API health, RoomHub connection, and a link to the reference slice.
 * Delete or hide it once real screens cover these paths.
 */
export function DevStatusPage() {
  const health = useHealth()
  const hub = useRoomHub()

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-extrabold">Plumbing status</h1>
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 font-mono text-sm">
        <dt>API</dt>
        <dd data-testid="api-status">{health.isPending ? 'checking…' : health.isError ? 'unreachable' : health.data.status}</dd>
        <dt>RoomHub</dt>
        <dd data-testid="hub-status">{hub.status}</dd>
      </dl>
      <Link to="/films/550" className="font-bold text-pink-text">
        Reference slice: /films/550
      </Link>
    </section>
  )
}
