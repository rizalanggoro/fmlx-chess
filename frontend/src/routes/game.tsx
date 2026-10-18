import { Button } from '@/components/ui/button'
import {
  getGameBoardOptions,
  getGameHistoryOptions,
  getGameLegalMovesOptions,
  getGameOptions,
  postGameMovesMutation,
  postGameResetMutation,
  postGameResignMutation,
  postGameStartMutation,
} from '@/lib/api'
import type { GameEndReason, PieceColor, PieceType, Position } from '@/lib/api'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useEffect, useRef, useState } from 'react'

export const Route = createFileRoute('/game')({ component: Game })

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

const same = (a: Position | null, b: Position) =>
  a !== null && a.column === b.column && a.row === b.row

const alg = (p: Position) => `${'abcdefgh'[p.column]}${p.row + 1}`

function Game() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [selected, setSelected] = useState<Position | null>(null)
  const state = useQuery(getGameOptions())
  const board = useQuery(getGameBoardOptions())
  const history = useQuery(getGameHistoryOptions())
  const legal = useQuery({
    ...getGameLegalMovesOptions({ query: { column: selected?.column, row: selected?.row } }),
    enabled: selected !== null,
  })
  const makeMove = useMutation(postGameMovesMutation())
  const start = useMutation(postGameStartMutation())
  const reset = useMutation(postGameResetMutation())
  const resign = useMutation(postGameResignMutation())
  const started = useRef(false)

  // Direct open /game without starting first → auto-start once.
  useEffect(() => {
    if (state.data?.state === 'NotStarted' && !started.current && !start.isPending) {
      started.current = true
      start.mutate({})
    }
  }, [state.data?.state, start])

  const pieces = new Map((board.data ?? []).map((t) => [`${t.column},${t.row}`, t]))
  const targets = new Set((legal.data ?? []).map((m) => `${m.to.column},${m.to.row}`))
  const turn = state.data?.currentPlayer.color
  const over = state.data?.state === 'Completed'

  const click = (sq: Position) => {
    if (state.data?.state !== 'InProgress' || makeMove.isPending) return
    const key = `${sq.column},${sq.row}`
    if (selected && targets.has(key)) {
      makeMove.mutate({ body: { from: selected, to: sq, promotionType: null } })
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

  const leave = async () => {
    await reset.mutateAsync({})
    await qc.invalidateQueries()
    await navigate({ to: '/' })
  }

  return (
    <div className="mx-auto flex min-h-svh max-w-5xl flex-col items-center gap-4 p-4 md:flex-row md:items-start">
      <div className="grid w-full max-w-xl flex-1 grid-cols-8 overflow-hidden rounded-lg border">
        {[7, 6, 5, 4, 3, 2, 1, 0].map((row) =>
          [0, 1, 2, 3, 4, 5, 6, 7].map((column) => {
            const sq = { column, row }
            const key = `${column},${row}`
            const piece = pieces.get(key)
            const dark = (column + row) % 2 === 1
            return (
              <button
                key={key}
                type="button"
                onClick={() => click(sq)}
                className={`relative flex aspect-square items-center justify-center text-4xl ${
                  dark ? 'bg-amber-700' : 'bg-amber-100'
                } ${same(selected, sq) ? 'ring-4 ring-inset ring-sky-500' : ''}`}
              >
                {piece?.color && piece.type && (
                  <span
                    className={
                      piece.color === 'White'
                        ? 'text-white drop-shadow-[0_1px_1px_rgba(0,0,0,0.9)]'
                        : 'text-neutral-900'
                    }
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

      <div className="flex w-full flex-col gap-4 md:w-72">
        <div className="rounded-lg border p-4">
          {state.data &&
            (over && state.data.result ? (
              <p className="font-semibold">
                {state.data.result.winner
                  ? `${state.data.result.winner} menang — ${REASONS[state.data.result.reason]}`
                  : REASONS[state.data.result.reason]}
              </p>
            ) : (
              <p>
                Giliran: <span className="font-semibold">{turn}</span>
              </p>
            ))}
          <p className="text-sm text-muted-foreground">Langkah: {state.data?.moveCount ?? 0}</p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button size="sm" disabled={start.isPending} onClick={() => start.mutate({})}>
            Game baru
          </Button>
          <Button size="sm" variant="outline" onClick={() => void leave()}>
            Lobby
          </Button>
          {!over && turn && (
            <Button
              size="sm"
              variant="destructive"
              disabled={resign.isPending}
              onClick={() => resign.mutate({ body: { color: turn } })}
            >
              {turn} menyerah
            </Button>
          )}
        </div>

        <div className="rounded-lg border p-4">
          <p className="mb-2 font-semibold">Riwayat</p>
          <ol className="max-h-64 space-y-1 overflow-y-auto text-sm">
            {(history.data ?? []).map((h, i) => (
              <li key={i}>
                {i + 1}. {h.playedBy[0]} {alg(h.move.from)}→{alg(h.move.to)}
                {h.move.promotionType ? `=${h.move.promotionType[0]}` : ''}
              </li>
            ))}
          </ol>
        </div>
      </div>
    </div>
  )
}
