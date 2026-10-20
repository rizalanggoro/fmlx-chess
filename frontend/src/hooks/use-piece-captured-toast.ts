import { toast } from '@/components/ui/toast'
import {
  getRoomsByRoomIdQueryKey,
  type PieceCapturedDto,
  type PieceColor,
  type PieceType,
  type RoomStatusDto,
} from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

const colorLabel = (color: PieceColor) =>
  color === 'White' ? 'Putih' : 'Hitam'

const pieceLabel: Record<PieceType, string> = {
  King: 'Raja',
  Queen: 'Ratu',
  Rook: 'Benteng',
  Bishop: 'Uskup',
  Knight: 'Kuda',
  Pawn: 'Pion',
}

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
      const name = byColor === 'White' ? room?.whiteName : room?.blackName
      const by = name?.trim()
        ? `${colorLabel(byColor)} (${name.trim()})`
        : colorLabel(byColor)

      toast.add({
        type: 'warning',
        title: `${pieceLabel[event.type]} kamu dimakan`,
        description: `Dimakan oleh ${by}.`,
      })
    }

    connection.on('PieceCaptured', onPieceCaptured)
    return () => connection.off('PieceCaptured', onPieceCaptured)
  }, [roomId, myColor, qc])
}
