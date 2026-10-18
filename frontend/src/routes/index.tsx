import { Button } from '@/components/ui/button'
import { postRoomsByRoomIdJoinMutation, postRoomsMutation, type RoomSessionDto } from '@/lib/api'
import { saveRoomSession } from '@/lib/roomSession'
import { useMutation } from '@tanstack/react-query'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'

export const Route = createFileRoute('/')({ component: Lobby })

const joinError = (error: unknown) => {
  const message =
    typeof error === 'object' && error !== null && 'message' in error
      ? String((error as { message: unknown }).message)
      : ''
  if (message === 'Room not found') return 'Room tidak ditemukan. Cek lagi kodenya.'
  if (message === 'Room is full') return 'Room sudah penuh.'
  return 'Gagal bergabung. Pastikan backend jalan.'
}

function Lobby() {
  const navigate = useNavigate()
  const [code, setCode] = useState('')
  const create = useMutation(postRoomsMutation())
  const join = useMutation(postRoomsByRoomIdJoinMutation())

  const enter = (res: RoomSessionDto) => {
    if (res.token && res.color) saveRoomSession(res.id, { token: res.token, color: res.color })
    void navigate({ to: '/room/$roomId', params: { roomId: res.id } })
  }

  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-8 p-4">
      <h1 className="text-4xl font-bold">fmlx-chess</h1>
      <div className="flex flex-col items-center gap-6">
        <Button
          size="lg"
          disabled={create.isPending}
          onClick={() => create.mutate({ body: { mode: 'Hotseat' } }, { onSuccess: enter })}
        >
          Main Hotseat
        </Button>

        <div className="flex w-72 flex-col gap-3 rounded-lg border p-4">
          <p className="text-sm font-semibold">Main Online</p>
          <Button
            size="sm"
            disabled={create.isPending}
            onClick={() => create.mutate({ body: { mode: 'Online' } }, { onSuccess: enter })}
          >
            Buat Room
          </Button>
          <div className="flex gap-2">
            <input
              value={code}
              onChange={(e) =>
                setCode(
                  e.target.value
                    .toUpperCase()
                    .replace(/[^A-Z0-9]/g, '')
                    .slice(0, 6),
                )
              }
              placeholder="Kode room"
              className="h-9 w-full min-w-0 rounded-md border bg-transparent px-3 font-mono text-sm tracking-widest outline-none placeholder:font-sans placeholder:tracking-normal focus-visible:ring-[3px] focus-visible:ring-ring/50"
            />
            <Button
              size="sm"
              variant="outline"
              disabled={code.length < 6 || join.isPending}
              onClick={() => join.mutate({ path: { roomId: code } }, { onSuccess: enter })}
            >
              Gabung
            </Button>
          </div>
          {join.isError && <p className="text-sm text-red-500">{joinError(join.error)}</p>}
        </div>
      </div>
      {create.isError && (
        <p className="text-sm text-red-500">Gagal membuat room. Pastikan backend jalan.</p>
      )}
    </div>
  )
}
