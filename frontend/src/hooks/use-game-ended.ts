import { toast } from '@/components/ui/toast'
import { REASONS } from '@/constants/chess'
import {
  getRoomsByRoomIdGameQueryKey,
  getRoomsByRoomIdQueryKey,
  type GameEndedDto,
  type GameStateDto,
  type PieceColor,
  type RoomStatusDto,
} from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

const colorLabel = (color: PieceColor) => (color === 'White' ? 'Putih' : 'Hitam')

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
        const name =
          event.result.winner === 'White' ? room?.whiteName : room?.blackName
        const player = name?.trim()
          ? `${colorLabel(event.result.winner)} (${name.trim()})`
          : colorLabel(event.result.winner)
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
