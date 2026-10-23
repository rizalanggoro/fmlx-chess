import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { GLYPHS } from '@/constants/chess'
import type { PieceColor, PieceType } from '@/lib/api'
import { cn } from 'cn'
import type { ReactNode } from 'react'

const NAMES: Record<PieceType, string> = {
  King: 'Raja',
  Queen: 'Ratu',
  Rook: 'Benteng',
  Bishop: 'Uskup',
  Knight: 'Kuda',
  Pawn: 'Pion',
}

const ORDER: PieceType[] = ['Queen', 'Rook', 'Bishop', 'Knight', 'Pawn']

export function CapturedPiecesDialog({
  color,
  playerName,
  pieces,
  className,
  children,
}: {
  color: PieceColor
  playerName: string
  pieces: PieceType[]
  className?: string
  children: ReactNode
}) {
  const capturedColor: PieceColor = color === 'White' ? 'Black' : 'White'
  const counts = ORDER.map((type) => ({
    type,
    count: pieces.filter((piece) => piece === type).length,
  })).filter(({ count }) => count > 0)

  return (
    <Dialog>
      <DialogTrigger
        className={cn(
          'flex w-full cursor-pointer items-center gap-3 rounded-xl border px-3 py-2 text-left outline-none transition-colors hover:bg-muted/40 focus-visible:ring-3 focus-visible:ring-ring/30',
          className,
        )}
      >
        {children}
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="font-heading text-xl">
            Bidak yang dimakan {playerName}
          </DialogTitle>
          <DialogDescription>
            {pieces.length > 0
              ? `${pieces.length} bidak ${capturedColor === 'White' ? 'Putih' : 'Hitam'} berhasil dimakan.`
              : 'Belum ada bidak yang dimakan.'}
          </DialogDescription>
        </DialogHeader>
        {counts.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {counts.map(({ type, count }) => (
              <span
                key={type}
                className="flex items-center gap-2 rounded-xl border border-border px-3 py-2"
              >
                <span
                  aria-hidden
                  className="font-heading text-2xl leading-none text-foreground"
                >
                  {GLYPHS[type][capturedColor]}
                </span>
                <span className="text-sm font-medium text-foreground">{NAMES[type]}</span>
                <span className="text-sm text-muted-foreground">×{count}</span>
              </span>
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
