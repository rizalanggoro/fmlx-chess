import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'

export function DrawOfferDialog({
  open,
  offerer,
  pending,
  onRespond,
  onDismiss,
}: {
  open: boolean
  offerer: string
  pending: boolean
  onRespond: (accepted: boolean) => void
  onDismiss: () => void
}) {
  return (
    <AlertDialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onDismiss()
      }}
    >
      <AlertDialogContent size="sm">
        <AlertDialogHeader>
          <AlertDialogTitle>Tawaran draw</AlertDialogTitle>
          <AlertDialogDescription>
            {offerer} menawarkan permainan seri. Terima tawaran ini?
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={pending} onClick={() => onRespond(false)}>
            Tolak
          </AlertDialogCancel>
          <AlertDialogAction disabled={pending} onClick={() => onRespond(true)}>
            Terima
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
