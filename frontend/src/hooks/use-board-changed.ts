import { getRoomsByRoomIdGameBoardQueryKey, type BoardChangedDto, type TileDto } from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

export function useBoardChanged(roomId: string) {
  const qc = useQueryClient()

  useEffect(() => {
    const queryKey = getRoomsByRoomIdGameBoardQueryKey({ path: { roomId } })
    const onBoardChanged = (event: BoardChangedDto) => {
      if (event.roomId !== roomId) return
      void qc.cancelQueries({ queryKey, exact: true })
      qc.setQueryData<TileDto[]>(queryKey, event.tiles)
    }

    connection.on('BoardChanged', onBoardChanged)
    return () => connection.off('BoardChanged', onBoardChanged)
  }, [roomId, qc])
}
