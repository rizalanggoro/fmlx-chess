import { Brand, Eyebrow, Shell } from '@/components/room-chrome'
import { Button } from '@/components/ui/button'
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

const inputClass =
  'h-10 w-full rounded-lg border border-amber-950/20 bg-white/80 px-3 text-sm text-amber-950 outline-none placeholder:text-amber-800/50 focus-visible:border-amber-800/60 focus-visible:ring-[3px] focus-visible:ring-amber-800/20'

const primaryClass =
  'bg-amber-800 text-amber-50 hover:bg-amber-900 focus-visible:border-amber-900 focus-visible:ring-amber-800/30'

const quietClass =
  'border-amber-950/20 bg-white/70 text-amber-950 hover:bg-amber-100 hover:text-amber-950'

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
  const label = (color: PieceColor) => (nameOf(color) ? `${colorName(color)} (${nameOf(color)})` : colorName(color))

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
      <Shell>
        <div className="mx-auto flex w-full max-w-md flex-col items-center gap-6">
          <Brand />
          <section className="w-full rounded-2xl border border-amber-950/10 bg-white/70 p-6 text-center shadow-sm">
            <Eyebrow>Room</Eyebrow>
            <h2 className="mt-2 font-heading text-2xl text-amber-950">Room tidak ditemukan</h2>
            <p className="mt-2 text-sm text-amber-900/80">
              Cek lagi kodenya, atau buat room baru dari lobby.
            </p>
            <Button className={`mt-5 w-full ${primaryClass}`} onClick={toLobby}>
              Kembali ke Lobby
            </Button>
          </section>
        </div>
      </Shell>
    )
  }

  if (!room.data) {
    return (
      <Shell>
        <p className="text-center text-amber-900/80">Memuat…</p>
      </Shell>
    )
  }

  if (online && !session && !(room.data.whiteJoined && room.data.blackJoined)) {
    const seat: PieceColor = room.data.whiteJoined ? 'Black' : 'White'
    return (
      <Shell>
        <div className="mx-auto flex w-full max-w-md flex-col items-center gap-6">
          <Brand />
          <section className="w-full rounded-2xl border border-amber-950/10 bg-white/70 p-6 shadow-sm">
            <Eyebrow>Online</Eyebrow>
            <h2 className="mt-2 font-heading text-2xl text-amber-950">Gabung ke room</h2>
            <p className="mt-1 font-mono text-lg font-semibold tracking-[0.2em] text-amber-950">
              {roomId}
            </p>
            <p className="mt-3 text-sm text-amber-900/80">
              Kamu akan main sebagai{' '}
              <span className="font-semibold text-amber-950">{colorName(seat)}</span>{' '}
              <span aria-hidden className="font-heading text-base">
                {glyphOf(seat)}
              </span>
            </p>
            <input
              value={joinName}
              onChange={(e) => setJoinName(e.target.value.slice(0, 24))}
              placeholder="Nama kamu (opsional)"
              aria-label="Nama kamu"
              autoComplete="off"
              className={`mt-4 ${inputClass}`}
            />
            <div className="mt-4 flex gap-2">
              <Button
                className={`flex-1 ${primaryClass}`}
                disabled={join.isPending}
                onClick={takeSeat}
              >
                {join.isPending ? 'Menghubungkan…' : 'Gabung'}
              </Button>
              <Button variant="outline" className={quietClass} onClick={toLobby}>
                Lobby
              </Button>
            </div>
          </section>
        </div>
      </Shell>
    )
  }

  if (waiting) {
    return (
      <Shell>
        <div className="mx-auto flex w-full max-w-md flex-col items-center gap-6">
          <Brand />
          <section className="w-full rounded-2xl border border-amber-950/10 bg-white/70 p-6 text-center shadow-sm">
            <Eyebrow>Online</Eyebrow>
            <h2 className="mt-2 font-heading text-2xl text-amber-950">
              Menunggu lawan bergabung…
            </h2>
            <p className="mt-2 text-sm text-amber-900/80">Bagikan kode room ini ke lawan.</p>
            <p className="mt-4 font-mono text-3xl font-bold tracking-[0.25em] text-amber-950">
              {roomId}
            </p>
            <div className="mt-5 flex flex-wrap justify-center gap-2">
              <Button className={primaryClass} onClick={copyCode}>
                {copied ? 'Tersalin ✓' : 'Salin kode'}
              </Button>
              <Button variant="outline" className={quietClass} onClick={toLobby}>
                Lobby
              </Button>
            </div>
            {myColor && (
              <p className="mt-4 text-sm text-amber-900/80">
                Kamu main sebagai{' '}
                <span className="font-semibold text-amber-950">{label(myColor)}</span>
              </p>
            )}
          </section>
        </div>
      </Shell>
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
          <section className="rounded-2xl border border-amber-950/10 bg-white/70 p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <Eyebrow>{online ? 'Online' : 'Hotseat'}</Eyebrow>
              {online && (
                <p className="font-mono text-sm font-semibold tracking-[0.2em] text-amber-950">
                  {roomId}
                </p>
              )}
            </div>

            <div className="mt-4 flex flex-col gap-2">
              {(['White', 'Black'] as const).map((color) => {
                const active = !over && turn === color
                return (
                  <div
                    key={color}
                    className={`flex items-center gap-3 rounded-xl border px-3 py-2 ${
                      active ? 'border-amber-800/30 bg-amber-100' : 'border-transparent'
                    }`}
                  >
                    <span
                      aria-hidden
                      className="font-heading text-2xl leading-none text-amber-950"
                    >
                      {glyphOf(color)}
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-semibold text-amber-950">
                        {nameOf(color) ?? colorName(color)}
                      </p>
                      <p className="text-xs text-amber-800/70">
                        {colorName(color)}
                        {myColor === color && ' · kamu'}
                      </p>
                    </div>
                    {active && (
                      <span className="rounded-full bg-amber-800 px-2 py-0.5 text-[10px] font-semibold tracking-wide text-amber-50 uppercase">
                        Giliran
                      </span>
                    )}
                  </div>
                )
              })}
            </div>

            {over && state.data?.result && (
              <p className="mt-4 rounded-xl bg-amber-900 px-3 py-2 text-sm font-semibold text-amber-50">
                {state.data.result.winner
                  ? `${label(state.data.result.winner)} menang — ${REASONS[state.data.result.reason]}`
                  : `Seri — ${REASONS[state.data.result.reason]}`}
              </p>
            )}

            <p className="mt-3 text-xs text-amber-800/70">
              Langkah: {state.data?.moveCount ?? 0}
              {online && !myColor && ' · kamu penonton'}
            </p>
          </section>

          <div className="flex flex-wrap gap-2">
            {canAct && (
              <Button
                disabled={start.isPending}
                className={primaryClass}
                onClick={() => start.mutate({ path: { roomId } })}
              >
                Game baru
              </Button>
            )}
            <Button variant="outline" className={quietClass} onClick={toLobby}>
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

          <section className="rounded-2xl border border-amber-950/10 bg-white/70 p-5 shadow-sm">
            <div className="flex items-baseline justify-between gap-3">
              <h2 className="font-heading text-xl text-amber-950">Riwayat</h2>
              <p className="text-xs text-amber-800/70">{moves.length} langkah</p>
            </div>

            {moves.length === 0 ? (
              <p className="mt-3 text-sm text-amber-800/70">Belum ada langkah.</p>
            ) : (
              <>
                <div className="mt-3 grid grid-cols-[1.75rem_1fr_1fr] gap-2 border-b border-amber-950/10 pb-1 text-xs text-amber-800/70">
                  <span />
                  <span>Putih</span>
                  <span>Hitam</span>
                </div>
                <ol className="mt-1 flex max-h-72 flex-col overflow-y-auto font-mono text-sm text-amber-950">
                  {rounds.map((round, i) => (
                    <li
                      key={i}
                      className={`grid grid-cols-[1.75rem_1fr_1fr] gap-2 rounded-md px-1 py-0.5 ${
                        i % 2 === 1 ? 'bg-amber-950/[0.04]' : ''
                      }`}
                    >
                      <span className="text-amber-800/60">{i + 1}.</span>
                      {round.map((h, j) => (
                        <span key={j}>{fmtMove(h)}</span>
                      ))}
                    </li>
                  ))}
                </ol>
              </>
            )}
          </section>
        </aside>
      </main>
    </Shell>
  )
}
