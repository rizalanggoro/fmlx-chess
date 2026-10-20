import { zodResolver } from '@hookform/resolvers/zod'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { postRoomsMutation } from '@/lib/api'
import { saveRoomSession } from '@/lib/room-session'
import { useMutation } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'

const createRoomSchema = z.object({
  name: z.string().trim().max(24, 'Nama maksimal 24 karakter'),
  color: z.enum(['White', 'Black']),
})

type CreateRoomValues = z.infer<typeof createRoomSchema>

export function CreateRoomDialog() {
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const {
    mutate: create,
    isPending: isCreating,
    isError: createError
  } = useMutation({
    ...postRoomsMutation(),
  })
  const form = useForm<CreateRoomValues>({
    resolver: zodResolver(createRoomSchema),
    defaultValues: { name: '', color: 'White' },
  })

  const createRoom = (values: CreateRoomValues) =>
    create(
      { body: { mode: 'Online', color: values.color, name: values.name || null } },
      {
        onSuccess: (res) => {
          if (res.token && res.color) saveRoomSession(res.id, { token: res.token, color: res.color })
          void navigate({ to: '/rooms/$roomId', params: { roomId: res.id } })
        },
      },
    )

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button />}>Buat Room</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="font-heading text-xl">Buat room online</DialogTitle>
          <DialogDescription>Pilih warna, lalu bagikan kode room ke lawan.</DialogDescription>
        </DialogHeader>

        <form id="create-room-form" onSubmit={form.handleSubmit(createRoom)}>
          <FieldGroup>
            <Controller
              name="name"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="create-room-name">Nama kamu</FieldLabel>
                  <Input
                    {...field}
                    id="create-room-name"
                    placeholder="Opsional"
                    autoComplete="off"
                    aria-invalid={fieldState.invalid}
                    onChange={(e) => field.onChange(e.target.value.slice(0, 24))}
                  />
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />

            <Controller
              name="color"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel>Main sebagai</FieldLabel>
                  <ToggleGroup
                    value={[field.value]}
                    onValueChange={(next) => {
                      const picked = next[0]
                      if (picked) field.onChange(picked)
                    }}
                    variant="outline"
                    aria-label="Main sebagai"
                    className="w-full"
                  >
                    {(['White', 'Black'] as const).map((c) => (
                      <ToggleGroupItem
                        key={c}
                        value={c}
                        className="flex-1 aria-pressed:bg-primary! aria-pressed:text-primary-foreground! aria-pressed:hover:bg-primary!"
                      >
                        <span aria-hidden className="font-heading text-base leading-none">
                          {c === 'White' ? '♔' : '♚'}
                        </span>
                        {c === 'White' ? 'Putih' : 'Hitam'}
                      </ToggleGroupItem>
                    ))}
                  </ToggleGroup>
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
          </FieldGroup>
        </form>

        {createError && (
          <p className="text-sm text-destructive">Gagal membuat room. Pastikan backend jalan.</p>
        )}

        <DialogFooter>
          <DialogClose render={<Button variant="outline" />}>Batal</DialogClose>
          <Button type="submit" form="create-room-form" disabled={isCreating}>
            {isCreating ? 'Menyiapkan…' : 'Buat Room'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
