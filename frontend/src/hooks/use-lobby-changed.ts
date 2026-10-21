import { getRoomsQueryKey, type LobbyChangedDto } from '@/lib/api'
import { connection } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

export function useLobbyChanged() {
  const qc = useQueryClient()

  useEffect(() => {
    const onLobbyChanged = ({ rooms }: LobbyChangedDto) =>
      qc.setQueryData(getRoomsQueryKey(), rooms)

    connection.on('LobbyChanged', onLobbyChanged)
    return () => connection.off('LobbyChanged', onLobbyChanged)
  }, [qc])
}
