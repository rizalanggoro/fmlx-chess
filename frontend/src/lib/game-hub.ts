import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5004'

export const connection = new HubConnectionBuilder()
  .withUrl(`${apiUrl}/hubs/game`, { withCredentials: false })
  .withAutomaticReconnect()
  .build()

const groups = new Set<string>()
let startPromise: Promise<void> | null = null

export function startHub() {
  if (connection.state === HubConnectionState.Connected) return Promise.resolve()
  if (connection.state === HubConnectionState.Connecting) {
    return startPromise ?? Promise.resolve()
  }
  startPromise ??= connection.start().finally(() => {
    startPromise = null
  })
  return startPromise
}

export async function joinGroup(group: string) {
  groups.add(group)
  try {
    await startHub()
    if (!groups.has(group)) return
    await connection.invoke('JoinGroup', group)
    if (!groups.has(group)) {
      await connection.invoke('LeaveGroup', group).catch(() => {})
    }
  } catch {
    // The reconnect handler replays tracked groups once the connection is back.
  }
}

export async function leaveGroup(group: string) {
  groups.delete(group)
  if (connection.state !== HubConnectionState.Connected) return
  await connection.invoke('LeaveGroup', group).catch(() => {})
}

export async function rejoinGroups() {
  if (connection.state !== HubConnectionState.Connected) return
  await Promise.all(
    [...groups].map((group) =>
      connection.invoke('JoinGroup', group).catch(() => {}),
    ),
  )
}
