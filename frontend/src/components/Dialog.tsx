import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import { Button } from './Button.tsx'

interface DialogProps {
  open: boolean
  title: ReactNode
  onClose: () => void
  footer?: ReactNode
  children: ReactNode
}

/** A modal on the native <dialog> element: focus trapping and Escape come with it. */
export function Dialog({ open, title, onClose, footer, children }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog ref={ref} className="dialog" onClose={onClose}>
      {open && (
        <>
          <header className="dialog__header">
            <h2>{title}</h2>
          </header>
          <div className="dialog__body">{children}</div>
          {footer && <footer className="dialog__footer">{footer}</footer>}
        </>
      )}
    </dialog>
  )
}

interface ConfirmDialogProps {
  open: boolean
  title: ReactNode
  message: ReactNode
  confirmLabel: string
  tone?: 'primary' | 'danger'
  busy?: boolean
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmDialog({ open, title, message, confirmLabel, tone = 'primary', busy, onConfirm, onCancel }: ConfirmDialogProps) {
  return (
    <Dialog
      open={open}
      title={title}
      onClose={onCancel}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            Keep it
          </Button>
          <Button variant={tone} loading={busy} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <p className="muted">{message}</p>
    </Dialog>
  )
}
