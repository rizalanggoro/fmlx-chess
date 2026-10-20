import {toast} from '@/components/ui/toast'
import type {DrawOfferedDto, PieceColor} from '@/lib/api'
import {connection} from '@/lib/game-hub'
import {useEffect} from 'react'

const colorLabel = (color: PieceColor) => (color === 'White' ? 'Putih' : 'Hitam')

export function useDrawOfferToast(roomId: string, myColor?: PieceColor) {
  useEffect(() => {
    const onDrawOffered = (event: DrawOfferedDto) => {
      if (event.roomId !== roomId) return
      if (event.color === myColor) return

      const color = colorLabel(event.color)
      const name = event.name?.trim()
      const offerer = !name || name === event.color ? color : `${name} (${color})`

      toast.add({
        type: 'info',
        title: `${offerer} menawarkan draw`,
        description: 'Tawaran permainan seri. Menunggu respons lawan.',
      })
    }

    connection.on('DrawOffered', onDrawOffered)
    return () => connection.off('DrawOffered', onDrawOffered)
  }, [roomId, myColor])
}
