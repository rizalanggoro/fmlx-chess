import {zodResolver} from '@hookform/resolvers/zod'
import {CreateRoomDialog} from '@/components/create-room-dialog'
import {Brand, Eyebrow, Shell} from '@/components/room-chrome'
import {Button} from '@/components/ui/button'
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card'
import {Field, FieldError, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {getRoomsOptions, postRoomsByRoomIdJoinMutation, type RoomSessionDto, type RoomStatusDto,} from '@/lib/api'
import {LOBBY_GROUP, joinGroup, leaveGroup} from '@/lib/game-hub'
import {saveRoomSession} from '@/lib/room-session'
import {useMutation, useQuery, useQueryClient} from '@tanstack/react-query'
import {createFileRoute, useNavigate} from '@tanstack/react-router'
import {useEffect} from 'react'
import {Controller, useForm} from 'react-hook-form'
import {z} from 'zod'

const joinRoomSchema = z.object({
  name: z.string().trim().max(24, 'Nama maksimal 24 karakter'),
})

type JoinRoomValues = z.infer<typeof joinRoomSchema>

export const Route = createFileRoute('/rooms/')({
  component: OpenRooms
})

// Joining takes whichever seat is still free, so show that seat.
const freeSeat = (room: RoomStatusDto) => (room.whiteJoined ? 'Hitam' : 'Putih')
const hostName = (room: RoomStatusDto) =>
  (room.whiteJoined ? room.whiteName : room.blackName) ?? 'Tanpa nama'

function OpenRooms() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  useEffect(() => {
    void joinGroup(LOBBY_GROUP).then(() => void qc.invalidateQueries())
    return () => {
      void leaveGroup(LOBBY_GROUP)
    }
  }, [qc])
  const form = useForm<JoinRoomValues>({
    resolver: zodResolver(joinRoomSchema),
    defaultValues: {name: ''},
  })
  const {
    data: rooms = [],
    isError: openError,
    isLoading: openLoading
  } = useQuery({
    ...getRoomsOptions(),
  })
  const {
    mutate: join,
    isPending: joinPending,
    isError: joinError
  } = useMutation({
    ...postRoomsByRoomIdJoinMutation(),
  })

  const enter = (res: RoomSessionDto) => {
    if (res.color) saveRoomSession(res.id, {color: res.color})
    void navigate({to: '/rooms/$roomId', params: {roomId: res.id}})
  }

  const joinRoom = (roomId: string) =>
    form.handleSubmit((values) =>
      join(
        {path: {roomId}, query: {name: values.name || undefined}},
        {onSuccess: enter},
      ),
    )()

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
            <Controller
              name="name"
              control={form.control}
              render={({field, fieldState}) => (
                <Field data-invalid={fieldState.invalid} className="max-w-sm">
                  <FieldLabel htmlFor="join-room-name">Nama kamu</FieldLabel>
                  <Input
                    {...field}
                    id="join-room-name"
                    placeholder="Opsional"
                    autoComplete="off"
                    aria-invalid={fieldState.invalid}
                    onChange={(e) => field.onChange(e.target.value.slice(0, 24))}
                  />
                  {fieldState.invalid && <FieldError errors={[fieldState.error]}/>}
                </Field>
              )}
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
