import type { PieceColor } from '@/lib/api'

export type RoomSession = {
  color: PieceColor
}

export const saveRoomSession = (roomId: string, session: RoomSession) => {
  sessionStorage.setItem(`room:${roomId}`, JSON.stringify(session))
}

export const loadRoomSession = (roomId: string): RoomSession | null => {
  const raw = sessionStorage.getItem(`room:${roomId}`)
  return raw ? (JSON.parse(raw) as RoomSession) : null
}
