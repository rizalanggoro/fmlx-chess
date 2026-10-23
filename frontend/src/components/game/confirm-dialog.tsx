import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Spinner } from '@/components/ui/spinner'
import { useState, type ComponentProps } from 'react'

export function ConfirmDialog({
  label,
  className,
  variant,
  disabled,
  title,
  description,
  confirmLabel,
  confirmVariant,
  onConfirm,
}: {
  label: string
  className?: string
  variant?: ComponentProps<typeof Button>['variant']
  disabled?: boolean
  title: string
  description: string
  confirmLabel: string
  confirmVariant?: ComponentProps<typeof Button>['variant']
  onConfirm: () => void
}) {
  const [open, setOpen] = useState(false)

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger
        render={<Button className={className} variant={variant} disabled={disabled} />}
      >
        {disabled && <Spinner data-icon="inline-start" />}
        {label}
      </AlertDialogTrigger>
      <AlertDialogContent size="sm">
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Batal</AlertDialogCancel>
          <AlertDialogAction
            variant={confirmVariant}
            onClick={() => {
              setOpen(false)
              onConfirm()
            }}
          >
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
