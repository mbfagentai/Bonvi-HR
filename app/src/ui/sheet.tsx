import { useEffect, useRef, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { Button } from './button'

/**
 * Pastdan chiqadigan varaq (UX §E.2).
 *
 * Tanlash uchun ham, tasdiqlash uchun ham shu ishlatiladi. Bir vaqtda
 * FAQAT bittasi ochiladi va ichma-ich joylashmaydi — varaq ustidagi
 * varaqdan orqaga qaytish yo'li telefonda tushunarsiz bo'lib qoladi.
 *
 * ⚠️ Fokus varaq ichiga kiritiladi va o'sha yerda ushlab turiladi;
 * yopilganda uni ochgan tugmaga QAYTADI (UX §H.2).
 */
interface SheetProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
}

export function Sheet({ open, title, onClose, children }: SheetProps) {
  const { t } = useTranslation()
  const panelRef = useRef<HTMLDivElement | null>(null)
  const openerRef = useRef<Element | null>(null)

  useEffect(() => {
    if (!open) return undefined

    openerRef.current = document.activeElement
    const panel = panelRef.current
    if (panel) panel.focus()

    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        onClose()
        return
      }

      if (event.key !== 'Tab' || !panel) return

      // Fokus tuzog'i: varaq ichidagi birinchi va oxirgi element
      // halqa bo'lib ulanadi, aks holda Tab varaq ostidagi ekranga
      // tushib ketadi va u yerda ko'rinmas holda yuradi.
      const focusable = panel.querySelectorAll<HTMLElement>(
        'button, [href], input, textarea, select, [tabindex]:not([tabindex="-1"])',
      )
      if (focusable.length === 0) return

      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (!first || !last) return

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      const opener = openerRef.current
      if (opener instanceof HTMLElement) opener.focus()
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed bottom-0 left-0 right-0 top-0 z-40">
      <button
        type="button"
        aria-label={t('common.close')}
        onClick={onClose}
        className="absolute bottom-0 left-0 right-0 top-0 bg-black/40"
      />

      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        className="absolute bottom-0 left-0 right-0 mx-auto max-h-[85%] w-full max-w-app overflow-y-auto rounded-t-sheet bg-bg-elevated pb-safe shadow-[0_-8px_30px_rgba(0,0,0,0.18)]"
      >
        <div className="px-4 pb-2 pt-4">
          <h2 className="text-title3 font-semibold text-fg">{title}</h2>
        </div>
        <div className="px-4 pb-4">{children}</div>
      </div>
    </div>
  )
}

/**
 * Tasdiqlash varag'i.
 *
 * ⚠️ Butun ilovada ATIGI UCHTA amal tasdiq so'raydi (UX §E.6):
 * chiqish, so'rovni bekor qilish va to'ldirilgan formadan chiqish.
 * Belgilashning O'ZI tasdiq so'ramaydi — kuniga ikki marta bosiladigan
 * amalni ikkilantirish ilovaning butun ma'nosini yo'qotardi.
 */
interface ConfirmSheetProps {
  open: boolean
  title: string
  text?: string
  /** Tasdiqlovchi tugmadagi FE'L — hech qachon «OK» emas. */
  confirmLabel: string
  cancelLabel?: string
  danger?: boolean
  busy?: boolean
  onConfirm: () => void
  onClose: () => void
}

export function ConfirmSheet({
  open,
  title,
  text,
  confirmLabel,
  cancelLabel,
  danger = false,
  busy = false,
  onConfirm,
  onClose,
}: ConfirmSheetProps) {
  const { t } = useTranslation()

  return (
    <Sheet open={open} title={title} onClose={onClose}>
      {text ? <p className="mb-4 text-subhead text-fg-secondary server-text">{text}</p> : null}

      <div className="space-y-2">
        <Button
          variant={danger ? 'danger' : 'primary'}
          block
          busy={busy}
          onClick={onConfirm}
        >
          {confirmLabel}
        </Button>
        <Button variant="plain" block onClick={onClose}>
          {cancelLabel ?? t('common.cancel')}
        </Button>
      </div>
    </Sheet>
  )
}
