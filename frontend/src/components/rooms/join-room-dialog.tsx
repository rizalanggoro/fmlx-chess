import {zodResolver} from '@hookform/resolvers/zod'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {Field, FieldError, FieldGroup, FieldLabel,} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {toast} from '@/components/ui/toast'
import {postRoomsByRoomIdJoinMutation, type RoomStatusDto} from '@/lib/api'
import {saveRoomSession} from '@/lib/room-session'
import {useMutation} from '@tanstack/react-query'
import {useNavigate} from '@tanstack/react-router'
import {Controller, useForm} from 'react-hook-form'
import {z} from 'zod'
import {Spinner} from '@/components/ui/spinner'

const joinRoomSchema = z.object({
  name: z.string().trim().max(24, 'Nama maksimal 24 karakter'),
})

type JoinRoomValues = z.infer<typeof joinRoomSchema>

export function JoinRoomDialog(
  {
    room,
    onOpenChange
  }: {
    room: RoomStatusDto | null
    onOpenChange: (open: boolean) => void
  }
) {
  const navigate = useNavigate()
  const {
    mutate: join,
    isPending,
  } = useMutation({
    ...postRoomsByRoomIdJoinMutation(),
    onError: () => {
      toast.add({
        type: 'error',
        title: 'Gagal bergabung',
        description: 'Kemungkinan room tersebut baru saja penuh. Silakan pilih room lain.',
      })
    },
  })

  const form = useForm<JoinRoomValues>({
    resolver: zodResolver(joinRoomSchema),
    defaultValues: {name: ''},
  })

  const onSubmit = (values: JoinRoomValues) => {
    if (!room) return
    join(
      {path: {roomId: room.id}, query: {name: values.name || undefined, color: room.whiteJoined ? 'Black' : 'White'}},
      {
        onSuccess: (res) => {
          if (res.color) saveRoomSession(res.id, {color: res.color})
          void navigate({to: '/rooms/$roomId', params: {roomId: res.id}})
        },
      },
    )
  }

  const seat = room?.whiteJoined ? 'Hitam' : 'Putih'

  return (
    <Dialog open={!!room} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="font-heading text-xl">
            Gabung room {room?.id}
          </DialogTitle>
          <DialogDescription>
            Anda akan bermain sebagai {seat}. Isi nama, lalu konfirmasi untuk bergabung.
          </DialogDescription>
        </DialogHeader>

        <form id="join-room-form" onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="name"
              control={form.control}
              render={({field, fieldState}) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="join-room-name">Nama Anda</FieldLabel>
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
          </FieldGroup>
        </form>

        <DialogFooter>
          <DialogClose render={<Button variant="outline"/>}>Batal</DialogClose>
          <Button type="submit" form="join-room-form" disabled={isPending}>
            {isPending && <Spinner data-icon="inline-start"/>}
            Gabung
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
