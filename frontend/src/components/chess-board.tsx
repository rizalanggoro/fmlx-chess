import { Spinner } from '@/components/ui/spinner'
import { FILES, GLYPHS } from '@/constants/chess'
import type { Move, PieceColor, Position, TileDto } from '@/lib/api'
import { cn } from 'cn'
import { useEffect, useState, type CSSProperties } from 'react'

const same = (a: Position | null | undefined, b: Position) =>
  a !== null && a !== undefined && a.column === b.column && a.row === b.row

const alg = (p: Position) => `${FILES[p.column]}${p.row + 1}`

type ChessBoardProps = {
  tiles: TileDto[]
  selected?: Position | null
  loading?: Position | null
  targets?: ReadonlySet<string>
  lastMove?: Move | null
  flip?: boolean
  disabled?: boolean
  checked?: PieceColor | null
  className?: string
  onSelect?: (position: Position) => void
}

type Animation = {
  move: Move
  piece: TileDto
}

export function ChessBoard({
  tiles,
  selected,
  loading,
  targets,
  lastMove = null,
  flip = false,
  disabled = false,
  checked = null,
  className,
  onSelect,
}: ChessBoardProps) {
  const pieces = new Map(tiles.map((t) => [`${t.column},${t.row}`, t]))
  const checkedKing = checked
    ? tiles.find((tile) => tile.color === checked && tile.type === 'King')
    : undefined
  const rows = flip ? [0, 1, 2, 3, 4, 5, 6, 7] : [7, 6, 5, 4, 3, 2, 1, 0]
  const cols = flip ? [7, 6, 5, 4, 3, 2, 1, 0] : [0, 1, 2, 3, 4, 5, 6, 7]
  const [animation, setAnimation] = useState<Animation | null>(null)
  const [seenMove, setSeenMove] = useState<Move | null>(lastMove)
  const [previousTiles, setPreviousTiles] = useState(tiles)

  if (lastMove !== seenMove) {
    setSeenMove(lastMove)
    if (!lastMove) {
      setAnimation(null)
    } else {
      const piece = previousTiles.find(
        (tile) =>
          tile.column === lastMove.from.column && tile.row === lastMove.from.row,
      )
      setAnimation(piece ? { move: lastMove, piece } : null)
    }
  }

  if (tiles !== previousTiles) {
    setPreviousTiles(tiles)
  }

  useEffect(() => {
    if (!animation) return
    const timer = window.setTimeout(() => setAnimation(null), 300)
    return () => window.clearTimeout(timer)
  }, [animation])

  const from = animation?.move.from
  const to = animation?.move.to
  const fromColumn = from ? (flip ? 7 - from.column : from.column) : 0
  const fromRow = from ? (flip ? from.row : 7 - from.row) : 0
  const toColumn = to ? (flip ? 7 - to.column : to.column) : 0
  const toRow = to ? (flip ? to.row : 7 - to.row) : 0
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
        'mx-auto w-[min(100%,calc(100svh_-_12rem))] rounded-2xl border border-amber-950/20 bg-amber-900 p-3 shadow-sm sm:p-4',
        className,
      )}
    >
      <div className="@container relative">
        <div className="grid grid-cols-8 overflow-hidden rounded-[3px] border border-amber-950/40">
          {rows.map((row) =>
            cols.map((column) => {
              const sq = { column, row }
              const key = `${column},${row}`
              const piece = pieces.get(key)
              const dark = (column + row) % 2 === 1
              const coord = dark ? 'text-amber-100/60' : 'text-amber-900/50'
              const target = targets?.has(key) ?? false
              const loadingHere = same(loading, sq)
              const inCheck = checkedKing !== undefined && same(checkedKing, sq)
              const animating =
                animation !== null &&
                (same(animation.move.from, sq) || same(animation.move.to, sq))
              return (
                <button
                  key={key}
                  type="button"
                  onClick={() => {
                    if (!disabled) onSelect?.(sq)
                  }}
                  aria-disabled={disabled}
                  aria-busy={loadingHere}
                  aria-label={`${alg(sq)}${piece?.type ? ` ${piece.type}` : ''}${inCheck ? ' skak' : ''}`}
                  className={`relative flex aspect-square items-center justify-center ${
                    dark ? 'bg-amber-700' : 'bg-amber-100'
                  } ${same(selected, sq) ? 'ring-4 ring-inset ring-sky-500' : ''} ${
                    disabled ? 'cursor-default' : ''
                  }`}
                >
                  {inCheck && (
                    <span
                      aria-hidden
                      className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle,rgba(139,92,246,0.95)_0%,rgba(139,92,246,0.8)_40%,rgba(139,92,246,0.25)_70%,transparent_100%)]"
                    />
                  )}
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
                  {piece?.color && piece.type && !animating && (
                    <span
                      className={`relative z-10 text-[8.5cqw] leading-none ${
                        piece.color === 'White'
                          ? 'text-amber-50 drop-shadow-[0_1px_1px_rgba(80,50,10,0.9)]'
                          : 'text-amber-950'
                      } ${loadingHere ? 'opacity-40' : ''}`}
                    >
                      {GLYPHS[piece.type][piece.color]}
                    </span>
                  )}
                  {loadingHere && (
                    <span className="absolute inset-0 flex items-center justify-center">
                      <Spinner
                        aria-label="Memuat langkah legal"
                        className={`size-[26%] ${
                          dark ? 'text-amber-100/80' : 'text-amber-900/70'
                        }`}
                      />
                    </span>
                  )}
                  {target && !piece?.type && (
                    <span className="absolute h-3 w-3 rounded-full bg-sky-500/70" />
                  )}
                  {target && piece?.type && (
                    <span className="absolute inset-0 rounded-full ring-4 ring-inset ring-sky-500/70" />
                  )}
                </button>
              )
            }),
          )}
        </div>
        {animation?.piece.color && animation.piece.type && (
          <span
            aria-hidden
            className="chess-move pointer-events-none absolute z-10 flex items-center justify-center"
            style={moveStyle}
          >
            <span
              className={`text-[8.5cqw] leading-none ${
                animation.piece.color === 'White'
                  ? 'text-amber-50 drop-shadow-[0_1px_1px_rgba(80,50,10,0.9)]'
                  : 'text-amber-950'
              }`}
            >
              {GLYPHS[animation.piece.type][animation.piece.color]}
            </span>
          </span>
        )}
      </div>
    </section>
  )
}
