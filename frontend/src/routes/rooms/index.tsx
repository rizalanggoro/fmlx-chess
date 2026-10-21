import {CreateRoomDialog} from '@/components/create-room-dialog'
import {JoinRoomDialog} from '@/components/rooms/join-room-dialog'
import {Brand, Eyebrow, Shell} from '@/components/room-chrome'
import {Button} from '@/components/ui/button'
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {Spinner} from '@/components/ui/spinner'
import {getRoomsOptions, type RoomStatusDto} from '@/lib/api'
import {joinGroup, leaveGroup, LOBBY_GROUP} from '@/lib/game-hub'
import {useQuery, useQueryClient} from '@tanstack/react-query'
import {createFileRoute} from '@tanstack/react-router'
import {RiDoorOpenLine, RiErrorWarningLine} from '@remixicon/react'
import {useEffect, useState} from 'react'

export const Route = createFileRoute('/rooms/')({
  component: Page
})

// Joining takes whichever seat is still free, so show that seat.
const freeSeat = (room: RoomStatusDto) => (room.whiteJoined ? 'Hitam' : 'Putih')
const hostName = (room: RoomStatusDto) =>
  (room.whiteJoined ? room.whiteName : room.blackName) ?? 'Tanpa nama'

function Page() {
  const qc = useQueryClient()
  const [target, setTarget] = useState<RoomStatusDto | null>(null)

  useEffect(() => {
    void joinGroup(LOBBY_GROUP).then(() => void qc.invalidateQueries())
    return () => {
      void leaveGroup(LOBBY_GROUP)
    }
  }, [qc])

  const {
    data: rooms = [],
    isError: openError,
    isLoading: openLoading,
    isFetching: openFetching,
    refetch: refetchRooms
  } = useQuery({
    ...getRoomsOptions(),
  })

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
              Silakan pilih room yang sedang menunggu lawan. Anda akan otomatis memperoleh
              kursi yang kosong.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {openLoading ? (
              <div className="flex flex-col gap-2">
                {[0, 1, 2].map((i) => (
                  <div
                    key={i}
                    className="flex items-center gap-3 rounded-xl border border-border px-3 py-2"
                  >
                    <Skeleton className="h-5 w-16"/>
                    <Skeleton className="h-3 flex-1"/>
                    <Skeleton className="h-8 w-16"/>
                  </div>
                ))}
              </div>
            ) : openError ? (
              <Empty className="border border-dashed border-border py-10">
                <EmptyHeader>
                  <EmptyMedia variant="icon">
                    <RiErrorWarningLine/>
                  </EmptyMedia>
                  <EmptyTitle>Gagal memuat daftar room</EmptyTitle>
                  <EmptyDescription>
                    Pastikan layanan backend telah berjalan, kemudian silakan coba kembali.
                  </EmptyDescription>
                </EmptyHeader>
                <Button variant="outline" disabled={openFetching} onClick={() => void refetchRooms()}>
                  {openFetching && <Spinner data-icon="inline-start"/>}
                  Coba lagi
                </Button>
              </Empty>
            ) : rooms.length === 0 ? (
              <Empty className="border border-dashed border-border py-10">
                <EmptyHeader>
                  <EmptyMedia variant="icon">
                    <RiDoorOpenLine/>
                  </EmptyMedia>
                  <EmptyTitle>Belum ada room terbuka</EmptyTitle>
                  <EmptyDescription>
                    Silakan buat room baru, lalu bagikan kodenya kepada lawan.
                  </EmptyDescription>
                </EmptyHeader>
              </Empty>
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
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => setTarget(room)}
                    >
                      Gabung
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </main>

      <JoinRoomDialog
        room={target}
        onOpenChange={(open) => {
          if (!open) setTarget(null)
        }}
      />
    </Shell>
  )
}
