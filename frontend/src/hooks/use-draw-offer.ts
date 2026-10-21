import { toast } from '@/components/ui/toast'
import type { DrawDeclinedDto, DrawOfferedDto, PieceColor } from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useEffect } from 'react'

export function useDrawOffer(
  roomId: string,
  myColor?: PieceColor,
  onOffer?: (offer: DrawOfferedDto) => void,
) {
  useEffect(() => {
    const onDrawOffered = (event: DrawOfferedDto) => {
      if (event.roomId !== roomId || event.color === myColor) return
      onOffer?.(event)
    }

    const onDrawDeclined = (event: DrawDeclinedDto) => {
      if (event.roomId !== roomId || event.offerer !== myColor) return
      toast.add({
        type: 'info',
        title: 'Tawaran draw ditolak',
        description: 'Lawan menolak tawaran seri. Permainan lanjut.',
      })
    }

    connection.on('DrawOffered', onDrawOffered)
    connection.on('DrawDeclined', onDrawDeclined)
    return () => {
      connection.off('DrawOffered', onDrawOffered)
      connection.off('DrawDeclined', onDrawDeclined)
    }
  }, [roomId, myColor, onOffer])
}
