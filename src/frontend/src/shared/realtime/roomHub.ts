import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { ROOM_HUB_PATH } from './events'

/** SignalR's quick retries after a dropped connection. */
const AUTOMATIC_RECONNECT_MS = [0, 2_000, 10_000, 30_000]

/** Builds (does not start) a connection to RoomHub. Same origin; the dev proxy / nginx forwards /hubs with websockets. */
export function createRoomHubConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(ROOM_HUB_PATH)
    .withAutomaticReconnect(AUTOMATIC_RECONNECT_MS)
    // The default logger prints payloads and URLs; nothing in them is needed in the console.
    .configureLogging(LogLevel.None)
    .build()
}

export type RoomHubStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export function statusOf(connection: HubConnection): RoomHubStatus {
  switch (connection.state) {
    case HubConnectionState.Connected:
      return 'connected'
    case HubConnectionState.Connecting:
      return 'connecting'
    case HubConnectionState.Reconnecting:
      return 'reconnecting'
    default:
      return 'disconnected'
  }
}

/** Hub methods the client may invoke (docs/realtime.md → Hub). Membership is checked by the server before it joins the group. */
export const HUB_METHODS = { joinRoom: 'JoinRoom', leaveRoom: 'LeaveRoom' } as const
