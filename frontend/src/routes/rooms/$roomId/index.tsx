import { zodResolver } from '@hookform/resolvers/zod'
import { ChessBoard } from '@/components/chess-board'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { DrawOfferDialog } from '@/components/draw-offer-dialog'
import { Brand, Eyebrow, Shell, StateScreen } from '@/components/room-chrome'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Field, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  getRoomsByRoomIdGameBoardOptions,
  getRoomsByRoomIdGameHistoryOptions,
  getRoomsByRoomIdGameLegalMovesOptions,
  getRoomsByRoomIdGameOptions,
  getRoomsByRoomIdOptions,
  postRoomsByRoomIdGameDrawOfferMutation,
  postRoomsByRoomIdGameDrawRespondMutation,
  postRoomsByRoomIdGameMovesMutation,
  postRoomsByRoomIdGameResignMutation,
  postRoomsByRoomIdGameStartMutation,
  postRoomsByRoomIdJoinMutation,
} from '@/lib/api'
import { useBoardChanged } from '@/hooks/use-board-changed'
import { useCheckToast } from '@/hooks/use-check-toast'
import { useDrawOffer } from '@/hooks/use-draw-offer'
import { useGameEnded } from '@/hooks/use-game-ended'
import { usePieceCapturedToast } from '@/hooks/use-piece-captured-toast'
import { useTimerTick } from '@/hooks/use-timer-tick'
import { FILES, GLYPHS, REASONS } from '@/constants/chess'
import type { DrawOfferedDto, PieceColor, Position } from '@/lib/api'
import { joinGroup, leaveGroup, roomGroup } from '@/lib/game-hub'
import { loadRoomSession, saveRoomSession, type RoomSession } from '@/lib/room-session'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { Controller, useForm } from 'react-hook-form'
import { useEffect, useState } from 'react'
import { z } from 'zod'

const joinSchema = z.object({
  name: z.string().trim().max(24, 'Nama maksimal 24 karakter'),
})

type JoinValues = z.infer<typeof joinSchema>

export const Route = createFileRoute('/rooms/$roomId/')({ component: Room })

const colorName = (color: PieceColor) => (color === 'White' ? 'Putih' : 'Hitam')
const glyphOf = (color: PieceColor) => GLYPHS.King[color]

const same = (a: Position | null, b: Position) =>
  a !== null && a.column === b.column && a.row === b.row

const alg = (p: Position) => `${FILES[p.column]}${p.row + 1}`

function Room() {
  const { roomId } = Route.useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [selected, setSelected] = useState<Position | null>(null)
  const [session, setSession] = useState<RoomSession | null>(() => loadRoomSession(roomId))
  const [copied, setCopied] = useState(false)
  // ponytail: pending draw offer is local state (API exposes none); persist it in GameStateDto if it must survive reload.
  const [offer, setOffer] = useState<DrawOfferedDto | null>(null)
  const form = useForm<JoinValues>({
    resolver: zodResolver(joinSchema),
    defaultValues: { name: '' },
  })

  useEffect(() => {
    const group = roomGroup(roomId)
    void joinGroup(group).then(() => void qc.invalidateQueries())
    return () => {
      void leaveGroup(group)
    }
  }, [roomId, qc])

  useGameEnded(roomId)
  useBoardChanged(roomId)
  useDrawOffer(roomId, session?.color, setOffer)
  usePieceCapturedToast(roomId, session?.color)
  useCheckToast(roomId, session?.color)

  const {
    data: room,
    isError: roomError
  } = useQuery({
    ...getRoomsByRoomIdOptions({ path: { roomId } }),
  })
  const { data: state } = useQuery({
    ...getRoomsByRoomIdGameOptions({ path: { roomId } }),
  })
  const { secondsLeft, clock } = useTimerTick(roomId, state)
  const {
    data: board
  } = useQuery({
    ...getRoomsByRoomIdGameBoardOptions({ path: { roomId } }),
  })
  const {
    data: history
  } = useQuery({
    ...getRoomsByRoomIdGameHistoryOptions({ path: { roomId } }),
  })
  const {
    data: legal
  } = useQuery({
    ...getRoomsByRoomIdGameLegalMovesOptions({
      path: { roomId },
      query: { column: selected?.column, row: selected?.row },
    }),
    enabled: selected !== null,
  })

  const {
    mutate: makeMove,
    isPending: isMoving
  } = useMutation({
    ...postRoomsByRoomIdGameMovesMutation(),
  })
  const {
    mutate: start,
    isPending: isStarting
  } = useMutation({
    ...postRoomsByRoomIdGameStartMutation(),
  })
  const {
    mutate: resign,
    isPending: isResigning
  } = useMutation({
    ...postRoomsByRoomIdGameResignMutation(),
  })
  const {
    mutate: offerDraw,
    isPending: isOfferingDraw
  } = useMutation({
    ...postRoomsByRoomIdGameDrawOfferMutation(),
  })
  const {
    mutate: respondDraw,
    isPending: isRespondingDraw
  } = useMutation({
    ...postRoomsByRoomIdGameDrawRespondMutation(),
  })
  const {
    mutate: join,
    isPending: isJoining
  } = useMutation({
    ...postRoomsByRoomIdJoinMutation(),
  })

  const online = room?.mode === 'Online'
  const waiting = online && !(room?.whiteJoined && room?.blackJoined)
  const myColor = session?.color
  const turn = state?.currentPlayer.color
  const over = state?.state === 'Completed'
  const myTurn = !online || turn === myColor
  const canAct = !online || myColor !== undefined
  const resignColor = online ? myColor : turn

  const nameOf = (color: PieceColor) =>
    color === 'White' ? room?.whiteName : room?.blackName
  const label = (color: PieceColor) =>
    nameOf(color) ? `${colorName(color)} (${nameOf(color)})` : colorName(color)

  const respond = (accepted: boolean) => {
    if (!myColor) return
    respondDraw(
      { path: { roomId }, body: { color: myColor, accepted } },
      { onSuccess: () => setOffer(null) },
    )
  }

  // Joining takes whichever seat is still free.
  const takeSeat = (values: JoinValues) =>
    join(
      { path: { roomId }, query: { name: values.name || undefined } },
      {
        onSuccess: (res) => {
          if (!res.color) return
          const s: RoomSession = { color: res.color }
          saveRoomSession(roomId, s)
          setSession(s)
        },
        onError: () => void qc.invalidateQueries(),
      },
    )

  const copyCode = () =>
    navigator.clipboard?.writeText(roomId).then(
      () => {
        setCopied(true)
        setTimeout(() => setCopied(false), 1500)
      },
      () => {},
    )

  const pieces = new Map((board ?? []).map((t) => [`${t.column},${t.row}`, t]))
  const targets = new Set((legal ?? []).map((m) => `${m.to.column},${m.to.row}`))

  // Black sees the board from Black's side.
  const flip = myColor === 'Black'

  const moves = history ?? []
  const rounds = moves.reduce<(typeof moves)[]>((acc, move, i) => {
    if (i % 2 === 0) acc.push([move])
    else acc[acc.length - 1].push(move)
    return acc
  }, [])
  const fmtMove = (h: (typeof moves)[number]) =>
    `${alg(h.move.from)}→${alg(h.move.to)}${h.move.promotionType ? `=${h.move.promotionType[0]}` : ''}`

  const click = (sq: Position) => {
    if (state?.state !== 'InProgress' || isMoving || !myTurn) return
    const key = `${sq.column},${sq.row}`
    if (selected && targets.has(key)) {
      makeMove({
        path: { roomId },
        body: { from: selected, to: sq, promotionType: null },
        query: myColor ? { color: myColor } : undefined,
      })
      setSelected(null)
      return
    }
    const piece = pieces.get(key)
    if (piece?.color && piece.color === turn) {
      setSelected(same(selected, sq) ? null : sq)
    } else {
      setSelected(null)
    }
  }

  const toLobby = () => void navigate({ to: '/' })

  if (roomError) {
    return (
      <StateScreen>
        <Card className="w-full text-center">
          <CardHeader>
            <Eyebrow>Room</Eyebrow>
            <CardTitle className="text-2xl">Room tidak ditemukan</CardTitle>
            <CardDescription>Cek lagi kodenya, atau buat room baru dari lobby.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button className="w-full" onClick={toLobby}>
              Kembali ke Lobby
            </Button>
          </CardContent>
        </Card>
      </StateScreen>
    )
  }

  if (!room) {
    return (
      <Shell>
        <p className="text-center text-muted-foreground">Memuat…</p>
      </Shell>
    )
  }

  if (online && !session && !(room.whiteJoined && room.blackJoined)) {
    const seat: PieceColor = room.whiteJoined ? 'Black' : 'White'
    return (
      <StateScreen>
        <Card className="w-full">
          <CardHeader>
            <Eyebrow>Online</Eyebrow>
            <CardTitle className="text-2xl">Gabung ke room</CardTitle>
            <CardDescription>
              Kode room{' '}
              <span className="font-mono text-base font-semibold tracking-[0.2em] text-foreground">
                {roomId}
              </span>
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">
              Kamu akan main sebagai{' '}
              <span className="font-semibold text-foreground">{colorName(seat)}</span>{' '}
              <span aria-hidden className="font-heading text-base">
                {glyphOf(seat)}
              </span>
            </p>
            <form
              id="join-room-form"
              className="flex flex-col gap-4"
              onSubmit={form.handleSubmit(takeSeat)}
            >
              <Controller
                name="name"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="join-room-name">Nama kamu</FieldLabel>
                    <Input
                      {...field}
                      id="join-room-name"
                      placeholder="Opsional"
                      autoComplete="off"
                      aria-invalid={fieldState.invalid}
                      onChange={(e) => field.onChange(e.target.value.slice(0, 24))}
                    />
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
              <div className="flex gap-2">
                <Button type="submit" className="flex-1" disabled={isJoining}>
                  {isJoining ? 'Menghubungkan…' : 'Gabung'}
                </Button>
                <Button type="button" variant="outline" onClick={toLobby}>
                  Lobby
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      </StateScreen>
    )
  }

  if (waiting) {
    return (
      <StateScreen>
        <Card className="w-full text-center">
          <CardHeader>
            <Eyebrow>Online</Eyebrow>
            <CardTitle className="text-2xl">Menunggu lawan bergabung…</CardTitle>
            <CardDescription>Bagikan kode room ini ke lawan.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <p className="font-mono text-3xl font-bold tracking-[0.25em] text-foreground">
              {roomId}
            </p>
            <div className="flex flex-wrap justify-center gap-2">
              <Button onClick={copyCode}>{copied ? 'Tersalin ✓' : 'Salin kode'}</Button>
              <Button variant="outline" onClick={toLobby}>
                Lobby
              </Button>
            </div>
            {myColor && (
              <p className="text-sm text-muted-foreground">
                Kamu main sebagai{' '}
                <span className="font-semibold text-foreground">{label(myColor)}</span>
              </p>
            )}
          </CardContent>
        </Card>
      </StateScreen>
    )
  }

  return (
    <Shell fill>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <Brand />
      </header>

      <main className="grid grid-cols-1 gap-6 md:grid-cols-[minmax(0,1fr)_18rem] md:items-start lg:grid-cols-[minmax(0,1fr)_20rem]">
        <ChessBoard
          tiles={board ?? []}
          selected={selected}
          targets={targets}
          flip={flip}
          disabled={state?.state !== 'InProgress' || isMoving || !myTurn}
          onSelect={click}
        />

        <aside className="flex flex-col gap-4">
          <Card size="sm">
            <CardHeader>
              <Eyebrow>{online ? 'Online' : 'Hotseat'}</Eyebrow>
              {online && (
                <CardAction>
                  <span className="font-mono text-sm font-semibold tracking-[0.2em] text-foreground">
                    {roomId}
                  </span>
                </CardAction>
              )}
            </CardHeader>

            <CardContent className="flex flex-col gap-2">
              {(['White', 'Black'] as const).map((color) => {
                const active = !over && turn === color
                return (
                  <div
                    key={color}
                    className={`flex items-center gap-3 rounded-xl border px-3 py-2 ${
                      active ? 'border-primary/30 bg-primary/10' : 'border-transparent'
                    }`}
                  >
                    <span
                      aria-hidden
                      className="font-heading text-2xl leading-none text-foreground"
                    >
                      {glyphOf(color)}
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-semibold text-foreground">
                        {nameOf(color) ?? colorName(color)}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {colorName(color)}
                        {myColor === color && ' · kamu'}
                      </p>
                    </div>
                    {active && <Badge>Giliran</Badge>}
                    <span
                      className={`font-mono text-sm tabular-nums ${
                        active ? 'font-semibold text-foreground' : 'text-muted-foreground'
                      }`}
                    >
                      {clock(secondsLeft(color))}
                    </span>
                  </div>
                )
              })}

              {over && state?.result && (
                <p className="mt-2 rounded-xl bg-primary px-3 py-2 text-sm font-semibold text-primary-foreground">
                  {state.result.winner
                    ? `${label(state.result.winner)} menang — ${REASONS[state.result.reason]}`
                    : `Seri — ${REASONS[state.result.reason]}`}
                </p>
              )}

              <p className="mt-1 text-xs text-muted-foreground">
                Langkah: {state?.moveCount ?? 0}
                {online && !myColor && ' · kamu penonton'}
              </p>
            </CardContent>
          </Card>

          <div className="flex flex-col gap-2">
            <div className="flex gap-2">
              {canAct && (
                <ConfirmDialog
                  className="flex-1"
                  label="Game baru"
                  disabled={isStarting}
                  title="Mulai game baru?"
                  description="Papan dan riwayat langkah akan direset dari awal."
                  confirmLabel="Ya, mulai baru"
                  onConfirm={() => start({ path: { roomId } })}
                />
              )}
              <ConfirmDialog
                className="flex-1"
                label="Lobby"
                variant="outline"
                title="Keluar ke lobby?"
                description="Room ini tetap hidup — kamu bisa masuk lagi pakai kode room."
                confirmLabel="Keluar ke lobby"
                onConfirm={toLobby}
              />
            </div>
            {!over && canAct && resignColor && (
              <ConfirmDialog
                className="w-full"
                label={`${colorName(resignColor)} menyerah`}
                variant="destructive"
                disabled={isResigning}
                title="Menyerah?"
                description={`${colorName(resignColor)} akan dinyatakan kalah dan game selesai.`}
                confirmLabel="Ya, menyerah"
                confirmVariant="destructive"
                onConfirm={() =>
                  resign({
                    path: { roomId },
                    body: { color: resignColor },
                  })
                }
              />
            )}
            {online && !over && myColor && !offer && (
              <ConfirmDialog
                className="w-full"
                label="Tawarkan draw"
                variant="outline"
                disabled={isOfferingDraw}
                title="Tawarkan draw?"
                description="Lawan bisa menerima untuk mengakhiri game seri, atau menolak."
                confirmLabel="Ya, tawarkan"
                onConfirm={() => offerDraw({ path: { roomId }, body: { color: myColor } })}
              />
            )}
          </div>

          <DrawOfferDialog
            open={!!offer && !over}
            offerer={offer ? label(offer.color) : 'Lawan'}
            pending={isRespondingDraw}
            onRespond={respond}
            onDismiss={() => setOffer(null)}
          />

          <Card size="sm">
            <CardHeader>
              <CardTitle className="font-heading text-xl">Riwayat</CardTitle>
              <CardAction>
                <p className="text-xs text-muted-foreground">{moves.length} langkah</p>
              </CardAction>
            </CardHeader>
            <CardContent>
              {moves.length === 0 ? (
                <p className="text-sm text-muted-foreground">Belum ada langkah.</p>
              ) : (
                <ScrollArea className="max-h-64">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="h-8! w-8 px-1 text-xs" />
                        <TableHead className="h-8! px-1 text-xs">Putih</TableHead>
                        <TableHead className="h-8! px-1 text-xs">Hitam</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody className="font-mono">
                      {rounds.map((round, i) => (
                        <TableRow key={i}>
                          <TableCell className="p-1! text-muted-foreground">
                            {i + 1}.
                          </TableCell>
                          {round.map((h, j) => (
                            <TableCell key={j} className="p-1!">
                              {fmtMove(h)}
                            </TableCell>
                          ))}
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </ScrollArea>
              )}
            </CardContent>
          </Card>
        </aside>
      </main>
    </Shell>
  )
}
