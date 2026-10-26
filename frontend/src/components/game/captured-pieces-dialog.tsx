import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { PIECE_IMAGE, PIECE_NAMES } from '@/constants/chess'
import type { PieceColor, PieceType } from '@/lib/api'

const ORDER: PieceType[] = ['Queen', 'Rook', 'Bishop', 'Knight', 'Pawn']

export function CapturedPiecesDialog({
  open,
  onOpenChange,
  color,
  playerName,
  pieces,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  color: PieceColor
  playerName: string
  pieces: PieceType[]
}) {
  const capturedColor: PieceColor = color === 'White' ? 'Black' : 'White'
  const counts = ORDER.map((type) => ({
    type,
    count: pieces.filter((piece) => piece === type).length,
  })).filter(({ count }) => count > 0)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
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
                <img
                  src={PIECE_IMAGE[type][capturedColor]}
                  alt=""
                  className="size-7 select-none"
                />
                <span className="text-sm font-medium text-foreground">{PIECE_NAMES[type]}</span>
                <span className="text-sm text-muted-foreground">×{count}</span>
              </span>
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
