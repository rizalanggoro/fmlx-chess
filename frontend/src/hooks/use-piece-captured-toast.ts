import { toast } from '@/components/ui/toast'
import { PIECE_NAMES, playerLabel } from '@/constants/chess'
import {
  getRoomsByRoomIdQueryKey,
  type PieceCapturedDto,
  type PieceColor,
  type RoomStatusDto,
} from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

const opponent = (color: PieceColor): PieceColor =>
  color === 'White' ? 'Black' : 'White'

export function usePieceCapturedToast(roomId: string, myColor?: PieceColor) {
  const qc = useQueryClient()

  useEffect(() => {
    if (!myColor) return

    const onPieceCaptured = (event: PieceCapturedDto) => {
      if (event.roomId !== roomId || event.color !== myColor) return

      const room = qc.getQueryData<RoomStatusDto>(
        getRoomsByRoomIdQueryKey({ path: { roomId } }),
      )
      const byColor = opponent(event.color)
      const by = playerLabel(byColor, room?.whiteName, room?.blackName)

      toast.add({
        type: 'warning',
        title: `${PIECE_NAMES[event.type]} kamu dimakan`,
        description: `Dimakan oleh ${by}.`,
      })
    }

    connection.on('PieceCaptured', onPieceCaptured)
    return () => connection.off('PieceCaptured', onPieceCaptured)
  }, [roomId, myColor, qc])
}
