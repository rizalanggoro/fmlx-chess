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
  postRoomsByRoomIdGameMovesMutation,
  postRoomsByRoomIdGameResignMutation,
  postRoomsByRoomIdGameStartMutation,
  postRoomsByRoomIdJoinMutation,
} from '@/lib/api'
import type { GameEndReason, PieceColor, PieceType, Position } from '@/lib/api'
import { loadRoomSession, saveRoomSession, type RoomSession } from '@/lib/roomSession'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'

export const Route = createFileRoute('/room/$roomId')({ component: Room })

const GLYPHS: Record<PieceType, Record<PieceColor, string>> = {
  King: { White: '♔', Black: '♚' },
  Queen: { White: '♕', Black: '♛' },
  Rook: { White: '♖', Black: '♜' },
  Bishop: { White: '♗', Black: '♝' },
  Knight: { White: '♘', Black: '♞' },
  Pawn: { White: '♙', Black: '♟' },
}

const REASONS: Record<GameEndReason, string> = {
  Checkmate: 'Checkmate',
  DrawByStalemate: 'Stalemate',
  DrawByAgreement: 'Draw disepakati',
  DrawBy50MoveRule: 'Aturan 50 langkah',
  DrawBy75MoveRule: 'Aturan 75 langkah',
  DrawByDeadPosition: 'Posisi mati',
  DrawByThreefoldRepetition: 'Repetisi 3x',
  DrawByFivefoldRepetition: 'Repetisi 5x',
  PlayerResigned: 'Menyerah',
  TimeExpired: 'Waktu habis',
}

const FILES = 'abcdefgh'
const colorName = (color: PieceColor) => (color === 'White' ? 'Putih' : 'Hitam')
const glyphOf = (color: PieceColor) => (color === 'White' ? '♔' : '♚')

const same = (a: Position | null, b: Position) =>
  a !== null && a.column === b.column && a.row === b.row

const alg = (p: Position) => `${FILES[p.column]}${p.row + 1}`

function Room() {
  const { roomId } = Route.useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [selected, setSelected] = useState<Position | null>(null)
  const [session, setSession] = useState<RoomSession | null>(() => loadRoomSession(roomId))
  const [joinName, setJoinName] = useState('')
  const [copied, setCopied] = useState(false)

  const room = useQuery(getRoomsByRoomIdOptions({ path: { roomId } }))
  const state = useQuery(getRoomsByRoomIdGameOptions({ path: { roomId } }))
  const board = useQuery(getRoomsByRoomIdGameBoardOptions({ path: { roomId } }))
  const history = useQuery(getRoomsByRoomIdGameHistoryOptions({ path: { roomId } }))
  const legal = useQuery({
    ...getRoomsByRoomIdGameLegalMovesOptions({
      path: { roomId },
      query: { column: selected?.column, row: selected?.row },
    }),
    enabled: selected !== null,
  })

  const makeMove = useMutation(postRoomsByRoomIdGameMovesMutation())
  const start = useMutation(postRoomsByRoomIdGameStartMutation())
  const resign = useMutation(postRoomsByRoomIdGameResignMutation())
  const join = useMutation(postRoomsByRoomIdJoinMutation())

  const online = room.data?.mode === 'Online'
  const waiting = online && !(room.data?.whiteJoined && room.data?.blackJoined)
  const myColor = session?.color
  const token = session?.token
  const turn = state.data?.currentPlayer.color
  const over = state.data?.state === 'Completed'
  const myTurn = !online || turn === myColor
  const canAct = !online || myColor !== undefined
  const resignColor = online ? myColor : turn
  const nameOf = (color: PieceColor) =>
    color === 'White' ? room.data?.whiteName : room.data?.blackName
  const label = (color: PieceColor) =>
    nameOf(color) ? `${colorName(color)} (${nameOf(color)})` : colorName(color)

  // Joining takes whichever seat is still free.
  const takeSeat = () =>
    join.mutate(
      { path: { roomId }, query: { name: joinName.trim() || undefined } },
      {
        onSuccess: (res) => {
          if (!res.token || !res.color) return
          const s: RoomSession = { token: res.token, color: res.color }
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

  const pieces = new Map((board.data ?? []).map((t) => [`${t.column},${t.row}`, t]))
  const targets = new Set((legal.data ?? []).map((m) => `${m.to.column},${m.to.row}`))

  // Black sees the board from Black's side.
  const flip = myColor === 'Black'
  const rows = flip ? [0, 1, 2, 3, 4, 5, 6, 7] : [7, 6, 5, 4, 3, 2, 1, 0]
  const cols = flip ? [7, 6, 5, 4, 3, 2, 1, 0] : [0, 1, 2, 3, 4, 5, 6, 7]

  const moves = history.data ?? []
  const rounds = moves.reduce<(typeof moves)[]>((acc, move, i) => {
    if (i % 2 === 0) acc.push([move])
    else acc[acc.length - 1].push(move)
    return acc
  }, [])
  const fmtMove = (h: (typeof moves)[number]) =>
    `${alg(h.move.from)}→${alg(h.move.to)}${h.move.promotionType ? `=${h.move.promotionType[0]}` : ''}`

  const click = (sq: Position) => {
    if (state.data?.state !== 'InProgress' || makeMove.isPending || !myTurn) return
    const key = `${sq.column},${sq.row}`
    if (selected && targets.has(key)) {
      makeMove.mutate({
        path: { roomId },
        body: { from: selected, to: sq, promotionType: null },
        query: token ? { token } : undefined,
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

  if (room.isError) {
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

  if (!room.data) {
    return (
      <Shell>
        <p className="text-center text-muted-foreground">Memuat…</p>
      </Shell>
    )
  }

  if (online && !session && !(room.data.whiteJoined && room.data.blackJoined)) {
    const seat: PieceColor = room.data.whiteJoined ? 'Black' : 'White'
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
            <Input
              value={joinName}
              onChange={(e) => setJoinName(e.target.value.slice(0, 24))}
              placeholder="Nama kamu (opsional)"
              aria-label="Nama kamu"
              autoComplete="off"
            />
            <div className="flex gap-2">
              <Button className="flex-1" disabled={join.isPending} onClick={takeSeat}>
                {join.isPending ? 'Menghubungkan…' : 'Gabung'}
              </Button>
              <Button variant="outline" onClick={toLobby}>
                Lobby
              </Button>
            </div>
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
        <section className="mx-auto w-full max-w-2xl rounded-2xl border border-amber-950/20 bg-amber-900 p-3 shadow-sm sm:p-4 lg:w-[min(100%,calc(100svh_-_12rem))] lg:max-w-none">
          <div className="@container grid grid-cols-8 overflow-hidden rounded-[3px] border border-amber-950/40">
            {rows.map((row) =>
              cols.map((column) => {
                const sq = { column, row }
                const key = `${column},${row}`
                const piece = pieces.get(key)
                const dark = (column + row) % 2 === 1
                const coord = dark ? 'text-amber-100/60' : 'text-amber-900/50'
                return (
                  <button
                    key={key}
                    type="button"
                    onClick={() => click(sq)}
                    aria-label={`${alg(sq)}${piece?.type ? ` ${piece.type}` : ''}`}
                    className={`relative flex aspect-square items-center justify-center ${
                      dark ? 'bg-amber-700' : 'bg-amber-100'
                    } ${same(selected, sq) ? 'ring-4 ring-inset ring-sky-500' : ''}`}
                  >
                    {column === cols[0] && (
                      <span
                        className={`absolute top-0.5 left-0.5 text-[10px] font-semibold ${coord}`}
                      >
                        {row + 1}
                      </span>
                    )}
                    {row === rows[7] && (
                      <span
                        className={`absolute right-0.5 bottom-0.5 text-[10px] font-semibold ${coord}`}
                      >
                        {FILES[column]}
                      </span>
                    )}
                    {piece?.color && piece.type && (
                      <span
                        className={`text-[8.5cqw] leading-none ${
                          piece.color === 'White'
                            ? 'text-amber-50 drop-shadow-[0_1px_1px_rgba(80,50,10,0.9)]'
                            : 'text-amber-950'
                        }`}
                      >
                        {GLYPHS[piece.type][piece.color]}
                      </span>
                    )}
                    {targets.has(key) && !piece?.type && (
                      <span className="absolute h-3 w-3 rounded-full bg-sky-500/70" />
                    )}
                    {targets.has(key) && piece?.type && (
                      <span className="absolute inset-0 rounded-full ring-4 ring-inset ring-sky-500/70" />
                    )}
                  </button>
                )
              }),
            )}
          </div>
        </section>

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
                  </div>
                )
              })}

              {over && state.data?.result && (
                <p className="mt-2 rounded-xl bg-primary px-3 py-2 text-sm font-semibold text-primary-foreground">
                  {state.data.result.winner
                    ? `${label(state.data.result.winner)} menang — ${REASONS[state.data.result.reason]}`
                    : `Seri — ${REASONS[state.data.result.reason]}`}
                </p>
              )}

              <p className="mt-1 text-xs text-muted-foreground">
                Langkah: {state.data?.moveCount ?? 0}
                {online && !myColor && ' · kamu penonton'}
              </p>
            </CardContent>
          </Card>

          <div className="flex flex-wrap gap-2">
            {canAct && (
              <Button
                disabled={start.isPending}
                onClick={() => start.mutate({ path: { roomId } })}
              >
                Game baru
              </Button>
            )}
            <Button variant="outline" onClick={toLobby}>
              Lobby
            </Button>
            {!over && canAct && resignColor && (
              <Button
                variant="destructive"
                disabled={resign.isPending}
                onClick={() =>
                  resign.mutate({
                    path: { roomId },
                    body: { color: resignColor },
                    query: token ? { token } : undefined,
                  })
                }
              >
                {colorName(resignColor)} menyerah
              </Button>
            )}
          </div>

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
