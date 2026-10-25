import { Eyebrow } from '@/components/room-chrome'
import { Badge } from '@/components/ui/badge'
import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
} from '@/components/ui/card'
import { colorName, GLYPHS, REASONS } from '@/constants/chess'
import { useTimerTick } from '@/hooks'
import type { GameStateDto, PieceColor } from '@/lib/api'

export function PlayerTurnCard({
  roomId,
  online,
  state,
  whiteName,
  blackName,
  myColor,
  onPlayerClick,
}: {
  roomId: string
  online: boolean
  state: GameStateDto | undefined
  whiteName?: string | null
  blackName?: string | null
  myColor?: PieceColor
  onPlayerClick: (color: PieceColor) => void
}) {
  const { secondsLeft, clock } = useTimerTick(roomId, state)
  const turn = state?.currentPlayer.color
  const over = state?.state === 'Completed'
  const nameOf = (color: PieceColor) =>
    color === 'White' ? whiteName : blackName
  const label = (color: PieceColor) =>
    nameOf(color) ? `${colorName(color)} (${nameOf(color)})` : colorName(color)

  return (
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
            <button
              key={color}
              type="button"
              aria-haspopup="dialog"
              onClick={() => onPlayerClick(color)}
              className={`flex w-full cursor-pointer items-center gap-3 rounded-xl border px-3 py-2 text-left outline-none transition-colors hover:bg-muted/40 focus-visible:ring-3 focus-visible:ring-ring/30 ${
                active ? 'border-primary/30 bg-primary/10' : 'border-transparent'
              }`}
            >
              <span
                aria-hidden
                className="font-heading text-2xl leading-none text-foreground"
              >
                {GLYPHS.King[color]}
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-semibold text-foreground">
                  {nameOf(color) ?? colorName(color)}
                </span>
                <span className="block text-xs text-muted-foreground">
                  {colorName(color)}
                  {myColor === color && ' · kamu'}
                </span>
              </span>
              <span className="sr-only">Lihat bidak yang dimakan</span>
              {active && <Badge>Giliran</Badge>}
              <span
                className={`font-mono text-sm tabular-nums ${
                  active ? 'font-semibold text-foreground' : 'text-muted-foreground'
                }`}
              >
                {clock(secondsLeft(color))}
              </span>
            </button>
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
  )
}
