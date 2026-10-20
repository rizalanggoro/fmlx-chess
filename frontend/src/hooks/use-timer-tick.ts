import { getRoomsByRoomIdGameQueryKey, type GameStateDto, type PieceColor, type TimerTickDto } from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

export function useTimerTick(roomId: string, state: GameStateDto | undefined) {
  const qc = useQueryClient()

  useEffect(() => {
    const queryKey = getRoomsByRoomIdGameQueryKey({ path: { roomId } })
    const onTick = (tick: TimerTickDto) => {
      if (tick.roomId !== roomId) return
      qc.setQueryData<GameStateDto>(queryKey, (prev) => {
        if (!prev) return prev
        return {
          ...prev,
          whiteRemainingSeconds:
            tick.color === 'White' ? tick.remainingSeconds : prev.whiteRemainingSeconds,
          blackRemainingSeconds:
            tick.color === 'Black' ? tick.remainingSeconds : prev.blackRemainingSeconds,
        }
      })
    }

    connection.on('TimerTick', onTick)
    return () => connection.off('TimerTick', onTick)
  }, [roomId, qc])

  const secondsLeft = (color: PieceColor) =>
    (color === 'White' ? state?.whiteRemainingSeconds : state?.blackRemainingSeconds) ?? 0
  const clock = (seconds: number) =>
    `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`

  return { secondsLeft, clock }
}
