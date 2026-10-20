import { getRoomsByRoomIdGameQueryKey, type GameStateDto } from '@/lib/api'
import { connection, type GameEnded } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

export function useGameEnded(roomId: string) {
  const qc = useQueryClient()

  useEffect(() => {
    const queryKey = getRoomsByRoomIdGameQueryKey({ path: { roomId } })
    const onGameEnded = (event: GameEnded) => {
      if (event.roomId !== roomId) return
      qc.setQueryData<GameStateDto>(queryKey, (prev) =>
        prev ? { ...prev, state: 'Completed', result: event.result } : prev,
      )
      void qc.invalidateQueries()
    }

    connection.on('GameEnded', onGameEnded)
    return () => connection.off('GameEnded', onGameEnded)
  }, [roomId, qc])
}
