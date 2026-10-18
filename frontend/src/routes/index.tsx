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

function BoardBand() {
  return (
    <div
      aria-hidden
      className="flex w-fit flex-col overflow-hidden rounded-[3px] border border-amber-950/20"
    >
      {[0, 1].map((row) => (
        <div key={row} className="flex">
          {Array.from({ length: 8 }, (_, i) => (
            <span
              key={i}
              className={`h-2 w-4 ${(i + row) % 2 === 0 ? 'bg-amber-100' : 'bg-amber-800'}`}
            />
          ))}
        </div>
      ))}
    </div>
  )
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

  const creating = create.variables?.body?.mode
  const createLabel = (mode: 'Hotseat' | 'Online', idle: string) =>
    create.isPending && creating === mode ? 'Menyiapkan…' : idle

  return (
    <div className="relative flex min-h-svh flex-col justify-center overflow-clip bg-amber-50 px-5 py-12 sm:px-8 md:px-10 md:py-16">
      <span
        aria-hidden
        className="pointer-events-none absolute -right-10 -bottom-12 hidden font-heading text-[22rem] leading-none text-amber-900/5 select-none lg:block"
      >
        ♞
      </span>

      <div className="relative mx-auto flex w-full max-w-3xl flex-col gap-10">
        <header className="flex flex-col items-start gap-4">
          <h1 className="font-heading text-5xl leading-none tracking-tight text-amber-950 sm:text-6xl md:text-7xl">
            fmlx<span className="text-amber-700">-</span>chess
          </h1>
          <p className="max-w-md text-amber-900/80">Papan catur untuk dua pemain.</p>
          <BoardBand />
        </header>

        <main className="grid grid-cols-1 gap-5 md:grid-cols-2">
          <section className="relative flex flex-col gap-4 overflow-hidden rounded-2xl border border-amber-950/20 bg-amber-900 p-6 text-amber-50 shadow-sm">
            <span
              aria-hidden
              className="pointer-events-none absolute right-2 bottom-20 font-heading text-8xl leading-none text-amber-50/10 select-none"
            >
              ♔
            </span>
            <p className="text-xs font-semibold tracking-[0.25em] text-amber-200/80 uppercase">
              Hotseat
            </p>
            <h2 className="font-heading text-2xl">Dua pemain, satu perangkat</h2>
            <p className="text-sm text-amber-100/80">
              Jalan bergantian di papan yang sama — langsung mulai, tanpa room.
            </p>
            <Button
              size="lg"
              className="mt-auto w-full bg-amber-50 text-amber-950 hover:bg-amber-100 focus-visible:border-amber-100 focus-visible:ring-amber-50/40"
              disabled={create.isPending}
              onClick={() => create.mutate({ body: { mode: 'Hotseat' } }, { onSuccess: enter })}
            >
              <span aria-hidden className="font-heading text-base leading-none">
                ♞
              </span>
              {createLabel('Hotseat', 'Main Hotseat')}
            </Button>
          </section>

          <section className="flex flex-col gap-4 rounded-2xl border border-amber-950/10 bg-white/70 p-6 shadow-sm">
            <p className="text-xs font-semibold tracking-[0.25em] text-amber-800/80 uppercase">
              Online
            </p>
            <h2 className="font-heading text-2xl text-amber-950">Main dari dua perangkat</h2>
            <p className="text-sm text-amber-900/80">
              Buat room, kirim kodenya ke lawan, lalu papan tersinkron otomatis.
            </p>
            <Button
              size="lg"
              className="w-full bg-amber-800 text-amber-50 hover:bg-amber-900 focus-visible:border-amber-900 focus-visible:ring-amber-800/30"
              disabled={create.isPending}
              onClick={() => create.mutate({ body: { mode: 'Online' } }, { onSuccess: enter })}
            >
              {createLabel('Online', 'Buat Room')}
            </Button>

            <div className="flex items-center gap-3 text-xs text-amber-800/80">
              <span aria-hidden className="h-px flex-1 bg-amber-950/15" />
              atau gabung pakai kode
              <span aria-hidden className="h-px flex-1 bg-amber-950/15" />
            </div>

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
                aria-label="Kode room"
                autoComplete="off"
                spellCheck={false}
                className="h-9 min-w-0 flex-1 rounded-lg border border-amber-950/20 bg-white/80 px-3 font-mono text-sm tracking-[0.2em] text-amber-950 uppercase outline-none placeholder:font-sans placeholder:tracking-normal placeholder:text-amber-800/50 focus-visible:border-amber-800/60 focus-visible:ring-[3px] focus-visible:ring-amber-800/20"
              />
              <Button
                variant="outline"
                className="border-amber-950/20 bg-white/70 text-amber-950 hover:bg-amber-100 hover:text-amber-950"
                disabled={code.length < 6 || join.isPending}
                onClick={() => join.mutate({ path: { roomId: code } }, { onSuccess: enter })}
              >
                {join.isPending ? 'Menghubungkan…' : 'Gabung'}
              </Button>
            </div>
            {join.isError && <p className="text-sm text-red-700">{joinError(join.error)}</p>}
          </section>
        </main>

        <footer className="flex flex-col gap-2 text-xs text-amber-800/80">
          <p>Tanpa akun · 10 menit per pemain · room hilang kalau server restart.</p>
          {create.isError && (
            <p className="text-sm text-red-700">Gagal membuat room. Pastikan backend jalan.</p>
          )}
        </footer>
      </div>
    </div>
  )
}
