import {CreateRoomDialog} from '@/components/create-room-dialog'
import {Brand, Eyebrow, Shell} from '@/components/room-chrome'
import {Button} from '@/components/ui/button'
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card'
import {Input} from '@/components/ui/input'
import {getRoomsOptions, postRoomsByRoomIdJoinMutation, type RoomSessionDto, type RoomStatusDto,} from '@/lib/api'
import {saveRoomSession} from '@/lib/roomSession'
import {useMutation, useQuery} from '@tanstack/react-query'
import {createFileRoute, useNavigate} from '@tanstack/react-router'
import {useState} from 'react'

export const Route = createFileRoute('/rooms/')({
  component: OpenRooms
})

// Joining takes whichever seat is still free, so show that seat.
const freeSeat = (room: RoomStatusDto) => (room.whiteJoined ? 'Hitam' : 'Putih')
const hostName = (room: RoomStatusDto) =>
  (room.whiteJoined ? room.whiteName : room.blackName) ?? 'Tanpa nama'

function OpenRooms() {
  const navigate = useNavigate()
  const [name, setName] = useState('')
  const {
    data: rooms = [],
    isError: openError,
    isLoading: openLoading
  } = useQuery(getRoomsOptions())
  const {
    mutate: join,
    isPending: joinPending,
    isError: joinError
  } = useMutation(postRoomsByRoomIdJoinMutation())

  const enter = (res: RoomSessionDto) => {
    if (res.token && res.color) saveRoomSession(res.id, {token: res.token, color: res.color})
    void navigate({to: '/rooms/$roomId', params: {roomId: res.id}})
  }

  const joinRoom = (roomId: string) =>
    join(
      {path: {roomId}, query: {name: name.trim() || undefined}},
      {onSuccess: enter},
    )

  return (
    <Shell>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <Brand/>
        <CreateRoomDialog/>
      </header>

      <main>
        <Card>
          <CardHeader>
            <Eyebrow>Online</Eyebrow>
            <CardTitle className="font-heading text-2xl">Room terbuka</CardTitle>
            <CardDescription>
              Pilih room yang sedang menunggu lawan — kamu otomatis dapat kursi yang kosong.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <Input
              value={name}
              onChange={(e) => setName(e.target.value.slice(0, 24))}
              placeholder="Nama kamu (opsional)"
              aria-label="Nama kamu"
              autoComplete="off"
              className="max-w-sm"
            />

            {openError ? (
              <p className="text-sm text-destructive">
                Gagal memuat daftar room. Pastikan backend jalan.
              </p>
            ) : rooms.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {openLoading
                  ? 'Memuat…'
                  : 'Belum ada room terbuka. Buat room baru, lalu bagikan kodenya ke lawan.'}
              </p>
            ) : (
              <ul className="flex flex-col gap-2">
                {rooms.map((room) => (
                  <li
                    key={room.id}
                    className="flex items-center gap-3 rounded-xl border border-border px-3 py-2"
                  >
                    <span className="font-mono text-sm font-semibold tracking-[0.2em] text-foreground">
                      {room.id}
                    </span>
                    <span className="min-w-0 flex-1 truncate text-xs text-muted-foreground">
                      {hostName(room)} · kamu jadi {freeSeat(room)}
                    </span>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={joinPending}
                      onClick={() => joinRoom(room.id)}
                    >
                      Gabung
                    </Button>
                  </li>
                ))}
              </ul>
            )}

            {joinError && (
              <p className="text-sm text-destructive">
                Gagal bergabung — mungkin room-nya baru saja penuh. Pilih room lain, ya.
              </p>
            )}
          </CardContent>
        </Card>
      </main>
    </Shell>
  )
}
