import { joinGroup, leaveGroup } from '@/lib/game-hub'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

export function useRoomGroup(roomId: string) {
  const qc = useQueryClient()

  useEffect(() => {
    const group = `room:${roomId}`
    void joinGroup(group).then(() => void qc.invalidateQueries())
    return () => {
      void leaveGroup(group)
    }
  }, [roomId, qc])
}
