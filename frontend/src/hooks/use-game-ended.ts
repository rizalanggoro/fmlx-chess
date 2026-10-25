import { toast } from '@/components/ui/toast'
import { playerLabel, REASONS } from '@/constants/chess'
import {
  getRoomsByRoomIdGameQueryKey,
  getRoomsByRoomIdQueryKey,
  type GameEndedDto,
  type GameStateDto,
  type RoomStatusDto,
} from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

export function useGameEnded(roomId: string) {
  const qc = useQueryClient()

  useEffect(() => {
    const queryKey = getRoomsByRoomIdGameQueryKey({ path: { roomId } })
    const onGameEnded = (event: GameEndedDto) => {
      if (event.roomId !== roomId) return
      qc.setQueryData<GameStateDto>(queryKey, (prev) =>
        prev ? { ...prev, state: 'Completed', result: event.result } : prev,
      )

      const room = qc.getQueryData<RoomStatusDto>(
        getRoomsByRoomIdQueryKey({ path: { roomId } }),
      )
      const reason = REASONS[event.result.reason]
      if (event.result.winner) {
        const player = playerLabel(event.result.winner, room?.whiteName, room?.blackName)
        toast.add({
          type: 'success',
          title: `${player} menang!`,
          description: reason,
        })
      } else {
        toast.add({
          type: 'info',
          title: 'Game seri',
          description: reason,
        })
      }
    }

    connection.on('GameEnded', onGameEnded)
    return () => connection.off('GameEnded', onGameEnded)
  }, [roomId, qc])
}
