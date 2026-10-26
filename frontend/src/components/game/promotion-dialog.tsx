import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {PIECE_IMAGE, PIECE_NAMES} from '@/constants/chess'
import type {PieceColor, PieceType} from '@/lib/api'

export function PromotionDialog(
  {
    open,
    color,
    options,
    pending,
    onSelect,
    onDismiss,
  }: {
    open: boolean
    color: PieceColor
    options: PieceType[]
    pending: boolean
    onSelect: (type: PieceType) => void
    onDismiss: () => void
  }
) {
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onDismiss()
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Promosi pion</DialogTitle>
          <DialogDescription>
            Pilih bidak pengganti pion yang mencapai baris terakhir.
          </DialogDescription>
        </DialogHeader>
        <div className="grid grid-cols-2 gap-2">
          {options.map((type) => (
            <Button
              key={type}
              type="button"
              variant="outline"
              disabled={pending}
              className="justify-start gap-3"
              onClick={() => onSelect(type)}
            >
              <img
                src={PIECE_IMAGE[type][color]}
                alt=""
                className="size-7 select-none"
              />
              {PIECE_NAMES[type]}
            </Button>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  )
}
