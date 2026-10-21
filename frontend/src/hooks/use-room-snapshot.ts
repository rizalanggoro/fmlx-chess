import {
  getRoomsByRoomIdGameBoardQueryKey,
  getRoomsByRoomIdGameHistoryQueryKey,
  getRoomsByRoomIdGameQueryKey,
  getRoomsByRoomIdQueryKey,
  type RoomSnapshotChangedDto,
} from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

// Satu event berisi seluruh state room; semua cache diisi dari payload yang sama.
export function useRoomSnapshot(roomId: string) {
  const qc = useQueryClient()

  useEffect(() => {
    const onSnapshot = (event: RoomSnapshotChangedDto) => {
      if (event.roomId !== roomId) return
      qc.setQueryData(getRoomsByRoomIdQueryKey({ path: { roomId } }), event.room)
      qc.setQueryData(getRoomsByRoomIdGameQueryKey({ path: { roomId } }), event.state)
      qc.setQueryData(getRoomsByRoomIdGameBoardQueryKey({ path: { roomId } }), event.board)
      qc.setQueryData(getRoomsByRoomIdGameHistoryQueryKey({ path: { roomId } }), event.history)
    }

    connection.on('RoomSnapshotChanged', onSnapshot)
    return () => connection.off('RoomSnapshotChanged', onSnapshot)
  }, [roomId, qc])
}
