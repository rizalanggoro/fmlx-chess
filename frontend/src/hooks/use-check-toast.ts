import { toast } from '@/components/ui/toast'
import { playerLabel } from '@/constants/chess'
import {
  getRoomsByRoomIdQueryKey,
  type CheckDeclaredDto,
  type PieceColor,
  type RoomStatusDto,
} from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

const opponent = (color: PieceColor): PieceColor =>
  color === 'White' ? 'Black' : 'White'

export function useCheckToast(roomId: string, myColor?: PieceColor) {
  const qc = useQueryClient()

  useEffect(() => {
    if (!myColor) return

    const onCheck = (event: CheckDeclaredDto) => {
      if (event.roomId !== roomId || event.color !== myColor) return

      const room = qc.getQueryData<RoomStatusDto>(
        getRoomsByRoomIdQueryKey({ path: { roomId } }),
      )
      const byColor = opponent(event.color)
      const by = playerLabel(byColor, room?.whiteName, room?.blackName)

      toast.add({
        type: 'error',
        title: 'Skak!',
        description: `Raja kamu dalam skak oleh ${by}.`,
      })
    }

    connection.on('CheckDeclared', onCheck)
    return () => connection.off('CheckDeclared', onCheck)
  }, [roomId, myColor, qc])
}
