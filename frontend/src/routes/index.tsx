import { BoardBand } from '@/components/board-band'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import {
  postRoomsByRoomIdJoinMutation,
  postRoomsMutation,
  type PieceColor,
  type RoomSessionDto,
} from '@/lib/api'
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
  const [name, setName] = useState('')
  const [color, setColor] = useState<PieceColor>('White')
  const create = useMutation(postRoomsMutation())
  const join = useMutation(postRoomsByRoomIdJoinMutation())

  const enter = (res: RoomSessionDto) => {
    if (res.token && res.color) saveRoomSession(res.id, { token: res.token, color: res.color })
    void navigate({ to: '/room/$roomId', params: { roomId: res.id } })
  }

  const creating = create.variables?.body?.mode
  const createLabel = (mode: 'Hotseat' | 'Online', idle: string) =>
    create.isPending && creating === mode ? 'Menyiapkan…' : idle

  return (
    <div className="relative flex min-h-svh flex-col justify-center bg-background px-5 py-12 sm:px-8 md:px-10 md:py-16">
      <span aria-hidden className="pointer-events-none absolute inset-0 overflow-clip">
        <span className="absolute -right-10 -bottom-12 hidden font-heading text-[22rem] leading-none text-foreground/5 select-none lg:block">
          ♞
        </span>
      </span>

      <div className="relative mx-auto flex w-full max-w-3xl flex-col gap-10">
        <header className="flex flex-col items-start gap-4">
          <h1 className="font-heading text-5xl leading-none tracking-tight text-foreground sm:text-6xl md:text-7xl">
            fmlx<span className="text-primary">-</span>chess
          </h1>
          <p className="max-w-md text-muted-foreground">Papan catur untuk dua pemain.</p>
          <BoardBand />
        </header>

        <main className="grid grid-cols-1 gap-5 md:grid-cols-2">
          <Card className="relative bg-primary! text-primary-foreground! ring-0">
            <span
              aria-hidden
              className="pointer-events-none absolute right-2 bottom-20 font-heading text-8xl leading-none text-primary-foreground/10 select-none"
            >
              ♔
            </span>
            <CardHeader>
              <CardDescription className="text-xs font-semibold tracking-[0.25em] text-primary-foreground/80 uppercase">
                Hotseat
              </CardDescription>
              <CardTitle className="font-heading text-2xl">Dua pemain, satu perangkat</CardTitle>
              <CardDescription className="text-primary-foreground/80">
                Jalan bergantian di papan yang sama — langsung mulai, tanpa room.
              </CardDescription>
            </CardHeader>
            <CardFooter className="mt-auto">
              <Button
                variant="secondary"
                size="lg"
                className="w-full"
                disabled={create.isPending}
                onClick={() => create.mutate({ body: { mode: 'Hotseat' } }, { onSuccess: enter })}
              >
                <span aria-hidden className="font-heading text-base leading-none">
                  ♞
                </span>
                {createLabel('Hotseat', 'Main Hotseat')}
              </Button>
            </CardFooter>
          </Card>

          <Card>
            <CardHeader>
              <CardDescription className="text-xs font-semibold tracking-[0.25em] uppercase">
                Online
              </CardDescription>
              <CardTitle className="font-heading text-2xl">Main dari dua perangkat</CardTitle>
              <CardDescription>
                Buat room, kirim kodenya ke lawan, lalu papan tersinkron otomatis.
              </CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-4">
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

              <Button
                size="lg"
                className="w-full"
                disabled={create.isPending}
                onClick={() =>
                  create.mutate(
                    { body: { mode: 'Online', color, name: name.trim() || null } },
                    { onSuccess: enter },
                  )
                }
              >
                {createLabel('Online', 'Buat Room')}
              </Button>

              <div className="flex items-center gap-3 text-xs text-muted-foreground">
                <Separator className="flex-1" />
                atau gabung pakai kode
                <Separator className="flex-1" />
              </div>

              <div className="flex gap-2">
                <Input
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
                  aria-label="Kode room"
                  autoComplete="off"
                  spellCheck={false}
                  className="min-w-0 flex-1 font-mono tracking-[0.2em] uppercase placeholder:font-sans placeholder:tracking-normal"
                />
                <Button
                  variant="outline"
                  disabled={code.length < 6 || join.isPending}
                  onClick={() =>
                    join.mutate(
                      { path: { roomId: code }, query: { name: name.trim() || undefined } },
                      { onSuccess: enter },
                    )
                  }
                >
                  {join.isPending ? 'Menghubungkan…' : 'Gabung'}
                </Button>
              </div>
              {join.isError && <p className="text-sm text-destructive">{joinError(join.error)}</p>}
            </CardContent>
          </Card>
        </main>

        <footer className="flex flex-col gap-2 text-xs text-muted-foreground">
          <p>Tanpa akun · 10 menit per pemain · room hilang kalau server restart.</p>
          {create.isError && (
            <p className="text-sm text-destructive">Gagal membuat room. Pastikan backend jalan.</p>
          )}
        </footer>
      </div>
    </div>
  )
}
