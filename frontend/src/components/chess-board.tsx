import {Spinner} from '@/components/ui/spinner'
import {FILES, PIECE_IMAGE, isSameMove, sameSquare} from '@/constants/chess'
import type {Move, PieceColor, Position, TileDto} from '@/lib/api'
import {cn} from 'cn'
import {useEffect, useMemo, useState, type CSSProperties} from 'react'

type ChessBoardProps = {
  tiles: TileDto[]
  selectedSquare?: Position | null
  pendingSquare?: Position | null
  legalTargets?: ReadonlySet<string>
  lastMove?: Move | null
  flipped?: boolean
  disabled?: boolean
  checkedColor?: PieceColor | null
  className?: string
  onSquareSelect?: (position: Position) => void
}

type Animation = {
  move: Move
  piece: TileDto
  captured?: TileDto
}

export function ChessBoard(
  {
    tiles,
    selectedSquare,
    pendingSquare,
    legalTargets,
    lastMove = null,
    flipped = false,
    disabled = false,
    checkedColor = null,
    className,
    onSquareSelect,
  }: ChessBoardProps) {
  const rows = flipped ? [0, 1, 2, 3, 4, 5, 6, 7] : [7, 6, 5, 4, 3, 2, 1, 0]
  const cols = flipped ? [7, 6, 5, 4, 3, 2, 1, 0] : [0, 1, 2, 3, 4, 5, 6, 7]
  const [animation, setAnimation] = useState<Animation | null>(null)
  const [promoteSquare, setPromoteSquare] = useState<Position | null>(null)
  const [seenMove, setSeenMove] = useState<Move | null>(lastMove)
  const [previousTiles, setPreviousTiles] = useState(tiles)

  if (!isSameMove(lastMove, seenMove)) {
    setSeenMove(lastMove)
    setPromoteSquare(lastMove?.promotionType ? lastMove.to : null)
    if (!lastMove) {
      setAnimation(null)
    } else {
      const piece =
        previousTiles.find((tile) => sameSquare(tile, lastMove.from)) ??
        tiles.find((tile) => sameSquare(tile, lastMove.from)) ??
        tiles.find((tile) => sameSquare(tile, lastMove.to))
      const captured =
        previousTiles.find(
          (tile) => sameSquare(tile, lastMove.to) && tile.color !== piece?.color,
        ) ??
        tiles.find(
          (tile) => sameSquare(tile, lastMove.to) && tile.color !== piece?.color,
        )
      setAnimation(piece ? {move: lastMove, piece, captured} : null)
    }
  }

  if (tiles !== previousTiles) {
    setPreviousTiles(tiles)
  }

  useEffect(() => {
    if (!animation) return
    const timer = window.setTimeout(() => setAnimation(null), 270)
    return () => window.clearTimeout(timer)
  }, [animation])

  const lastMovedPiece = animation?.piece ?? (lastMove ? previousTiles.find((t) => sameSquare(t, lastMove.from)) : undefined)

  const isTilesStale = Boolean(
    lastMove &&
    lastMovedPiece &&
    tiles.some((t) => sameSquare(t, lastMove.from) && t.color === lastMovedPiece.color)
  )

  const effectivePieces = useMemo(() => {
    const map = new Map(tiles.map((t) => [`${t.column},${t.row}`, t]))
    if (isTilesStale && lastMove && lastMovedPiece) {
      map.delete(`${lastMove.from.column},${lastMove.from.row}`)
      const targetKey = `${lastMove.to.column},${lastMove.to.row}`
      map.set(targetKey, {
        column: lastMove.to.column,
        row: lastMove.to.row,
        color: lastMovedPiece.color,
        type: lastMove.promotionType ?? lastMovedPiece.type,
      })
    }
    return map
  }, [tiles, isTilesStale, lastMove, lastMovedPiece])

  const checkedKing = checkedColor
    ? Array.from(effectivePieces.values()).find((tile) => tile.color === checkedColor && tile.type === 'King')
    : undefined

  useEffect(() => {
    if (!promoteSquare) return
    const timer = window.setTimeout(() => setPromoteSquare(null), 700)
    return () => window.clearTimeout(timer)
  }, [promoteSquare])

  const from = animation?.move.from
  const to = animation?.move.to
  const fromColumn = from ? (flipped ? 7 - from.column : from.column) : 0
  const fromRow = from ? (flipped ? from.row : 7 - from.row) : 0
  const toColumn = to ? (flipped ? 7 - to.column : to.column) : 0
  const toRow = to ? (flipped ? to.row : 7 - to.row) : 0
  const moveStyle =
    animation && from && to
      ? ({
        left: `${fromColumn * 12.5}%`,
        top: `${fromRow * 12.5}%`,
        width: '12.5%',
        height: '12.5%',
        '--move-x': `${(toColumn - fromColumn) * 100}%`,
        '--move-y': `${(toRow - fromRow) * 100}%`,
      } as CSSProperties)
      : undefined

  return (
    <section
      className={cn(
        'mx-auto w-[min(100%,calc(100svh_-_12rem))] rounded-[1.75rem] bg-linear-to-b from-amber-700 via-amber-800 to-amber-900 p-3 shadow-[0_20px_45px_-20px_rgba(69,26,3,0.75)] ring-1 ring-amber-950/40 sm:p-4',
        className,
      )}
    >
      <div className="@container relative">
        <div className="grid grid-cols-8 overflow-hidden rounded-xl border-2 border-amber-950/50 shadow-[inset_0_1px_0_rgba(255,255,255,0.12)]">
          {rows.map((row) =>
            cols.map((column) => {
              const sq = {column, row}
              const key = `${column},${row}`
              const piece = effectivePieces.get(key)
              const dark = (column + row) % 2 === 1
              const coord = dark ? 'text-amber-100/70' : 'text-amber-900/55'
              const target = legalTargets?.has(key) ?? false
              const pendingHere = sameSquare(pendingSquare, sq)
              const inCheck = checkedKing !== undefined && sameSquare(checkedKing, sq)
              const justMoved = lastMove !== null &&
                (sameSquare(lastMove.from, sq) || sameSquare(lastMove.to, sq))
              const animating = animation !== null &&
                (sameSquare(animation.move.from, sq) || sameSquare(animation.move.to, sq))
              const capturedPop = animation?.captured && sameSquare(animation.move.to, sq)
                ? animation.captured
                : null
              const promotingHere = sameSquare(promoteSquare, sq)

              return (
                <button
                  key={key}
                  type="button"
                  onClick={() => {
                    if (!disabled) onSquareSelect?.(sq)
                  }}
                  className={`group/square relative flex aspect-square items-center justify-center transition duration-150 ${
                    dark ? 'bg-amber-700' : 'bg-amber-100'
                  } ${sameSquare(selectedSquare, sq) ? 'ring-4 ring-inset ring-sky-500' : ''} ${
                    inCheck ? 'chess-check-shake' : ''
                  } ${
                    disabled ? 'cursor-default' : 'cursor-pointer hover:brightness-105 active:scale-95'
                  }`}
                >
                  {!disabled && (
                    <span className="pointer-events-none absolute inset-0 bg-white/0 transition-colors duration-100 group-active/square:bg-white/25"/>
                  )}
                  {justMoved && (
                    <span className="pointer-events-none absolute inset-0 bg-sky-400/30"/>
                  )}
                  {inCheck && (
                    <span
                      className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle,rgba(139,92,246,0.95)_0%,rgba(139,92,246,0.8)_40%,rgba(139,92,246,0.25)_70%,transparent_100%)]"
                    />
                  )}
                  {column === cols[0] && (
                    <span
                      className={`absolute top-1 left-1 rounded-md px-1 text-[10px] font-bold tabular-nums ${coord}`}
                    >
                      {row + 1}
                    </span>
                  )}
                  {row === rows[7] && (
                    <span
                      className={`absolute right-1 bottom-1 rounded-md px-1 text-[10px] font-bold uppercase ${coord}`}
                    >
                      {FILES[column]}
                    </span>
                  )}
                  {piece?.color && piece.type && !animating && (
                    <img
                      src={PIECE_IMAGE[piece.type][piece.color]}
                      alt=""
                      draggable={false}
                      className={`relative z-10 size-[8.5cqw] select-none transition-transform duration-150 drop-shadow-[0_2px_2px_rgba(69,26,3,0.35)] group-hover/square:-translate-y-[3%] group-hover/square:scale-105 ${
                        pendingHere ? 'opacity-40' : ''
                      } ${promotingHere ? 'chess-promote-pop' : ''}`}
                    />
                  )}
                  {capturedPop?.color && capturedPop.type && (
                    <img
                      src={PIECE_IMAGE[capturedPop.type][capturedPop.color]}
                      alt=""
                      draggable={false}
                      className="chess-capture-pop pointer-events-none absolute z-10 size-[8.5cqw] select-none"
                    />
                  )}
                  {pendingHere && (
                    <span className="absolute inset-0 flex items-center justify-center">
                      <Spinner
                        className={`size-[26%] ${
                          dark ? 'text-amber-100/80' : 'text-amber-900/70'
                        }`}
                      />
                    </span>
                  )}
                  {target && !piece?.type && (
                    <span className="absolute h-3 w-3 animate-pulse rounded-full bg-sky-500/70 motion-reduce:animate-none"/>
                  )}
                  {target && piece?.type && (
                    <span className="absolute inset-0 rounded-full ring-4 ring-inset ring-sky-500/70"/>
                  )}
                </button>
              )
            }),
          )}
        </div>
        {animation?.piece.color && animation.piece.type && (
          <span
            className="chess-move pointer-events-none absolute z-10 flex items-center justify-center"
            style={moveStyle}
          >
            <img
              src={PIECE_IMAGE[animation.piece.type][animation.piece.color]}
              alt=""
              draggable={false}
              className="size-[8.5cqw] select-none drop-shadow-[0_2px_2px_rgba(69,26,3,0.35)]"
            />
          </span>
        )}
      </div>
    </section>
  )
}
