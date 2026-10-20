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
import { Input } from '@/components/ui/input'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { postRoomsMutation, type PieceColor } from '@/lib/api'
import { saveRoomSession } from '@/lib/roomSession'
import { useMutation } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { useState } from 'react'

export function CreateRoomDialog() {
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [color, setColor] = useState<PieceColor>('White')
  const create = useMutation(postRoomsMutation())

  const createRoom = () =>
    create.mutate(
      { body: { mode: 'Online', color, name: name.trim() || null } },
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

        <div className="flex flex-col gap-4">
          <Input
            value={name}
            onChange={(e) => setName(e.target.value.slice(0, 24))}
            placeholder="Nama kamu (opsional)"
            aria-label="Nama kamu"
            autoComplete="off"
          />

          <ToggleGroup
            value={[color]}
            onValueChange={(next) => {
              const picked = next[0]
              if (picked) setColor(picked as PieceColor)
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

          {create.isError && (
            <p className="text-sm text-destructive">Gagal membuat room. Pastikan backend jalan.</p>
          )}
        </div>

        <DialogFooter>
          <DialogClose render={<Button variant="outline" />}>Batal</DialogClose>
          <Button disabled={create.isPending} onClick={createRoom}>
            {create.isPending ? 'Menyiapkan…' : 'Buat Room'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
