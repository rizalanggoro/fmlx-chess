import { BoardBand } from '@/components/board-band'
import { Button } from '@/components/ui/button'
import { Card, CardDescription, CardFooter, CardHeader, CardTitle, } from '@/components/ui/card'
import { Spinner } from '@/components/ui/spinner'
import { toast } from '@/components/ui/toast'
import { postRoomsMutation } from '@/lib/api'
import { saveRoomSession } from '@/lib/room-session'
import { useMutation } from '@tanstack/react-query'
import { createFileRoute, useNavigate } from '@tanstack/react-router'

export const Route = createFileRoute('/')({ component: Page })

function Page() {
  const navigate = useNavigate()
  const {
    mutate: createRoom,
    isPending: isCreating
  } = useMutation({
    ...postRoomsMutation(),
    onError: () => {
      toast.add({
        type: 'error',
        title: 'Gagal membuat room',
        description: 'Pastikan backend jalan.'
      })
    }
  })

  const onClickHotSeat = () =>
    createRoom(
      { body: { mode: 'HotSeat' } },
      {
        onSuccess: (res) => {
          if (res.color) saveRoomSession(res.id, { color: res.color })
          void navigate({ to: '/rooms/$roomId', params: { roomId: res.id } })
        },
      },
    )

  return (
    <div className="relative flex min-h-svh flex-col justify-center bg-background px-5 py-12 sm:px-8 md:px-10 md:py-16">
      <span aria-hidden className="pointer-events-none absolute inset-0 overflow-clip">
        <span
          className="absolute -right-10 -bottom-12 hidden font-heading text-[22rem] leading-none text-foreground/5 select-none lg:block">
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
                Jalan bergantian di papan yang sama. Langsung mulai, tanpa kode room.
              </CardDescription>
            </CardHeader>
            <CardFooter className="mt-auto">
              <Button
                variant="secondary"
                size="lg"
                className="w-full"
                disabled={isCreating}
                onClick={onClickHotSeat}
              >
                {isCreating ? (
                  <Spinner data-icon="inline-start" />
                ) : (
                  <span aria-hidden className="font-heading text-base leading-none">
                    ♞
                  </span>
                )}
                {isCreating ? 'Menyiapkan…' : 'Main Hotseat'}
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
                Buat atau pilih room, kirim kodenya ke lawan, lalu papan tersinkron otomatis.
              </CardDescription>
            </CardHeader>
            <CardFooter className="mt-auto">
              <Button
                variant="outline"
                size="lg"
                className="w-full"
                onClick={() => void navigate({ to: '/rooms' })}
              >
                <span aria-hidden className="font-heading text-base leading-none">
                  ♚
                </span>
                Lihat Room
              </Button>
            </CardFooter>
          </Card>
        </main>

        <footer className="flex flex-col gap-2 text-xs text-muted-foreground">
          <p>Tanpa akun · 10 menit per pemain</p>
        </footer>
      </div>
    </div>
  )
}
