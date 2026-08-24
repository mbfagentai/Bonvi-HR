import { X } from 'lucide-react'
import {
  useCallback,
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactNode,
} from 'react'
import { createPortal } from 'react-dom'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

const SIZES = {
  sm: 'sm:max-w-[440px]',
  md: 'sm:max-w-[560px]',
  lg: 'sm:max-w-[720px]',
} as const

/** Yopilish animatsiyasi tugagunicha panel DOM'da qoladi (globals.css: `pop-out`). */
const CLOSE_MS = 140

const FOCUSABLE = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',')

/**
 * Modal oyna.
 *
 * Nega Radix emas: loyihada faqat `dropdown-menu` va `tooltip` paketlari bor,
 * yangi bog'liqlik qo'shish esa lockfile'ni o'zgartiradi. Modal uchun kerak
 * bo'lgan uchta narsa — fokus qopqoni, Escape va varaqlash blokirovkasi —
 * shu yerda qo'lda bajarilgan.
 *
 * Ko'rinishi iOS'dagi kabi: tor ekranda pastdan ko'tariladigan varaq
 * (bottom sheet), keng ekranda markazdagi panel.
 */
export function Dialog({
  open,
  onClose,
  title,
  description,
  footer,
  children,
  size = 'md',
  /** Forma yuborilayotganda tasodifiy yopilishning oldini oladi. */
  dismissible = true,
}: {
  open: boolean
  onClose: () => void
  title: ReactNode
  description?: ReactNode
  footer?: ReactNode
  children: ReactNode
  size?: keyof typeof SIZES
  dismissible?: boolean
}) {
  const { t } = useLocale()
  const panelRef = useRef<HTMLDivElement>(null)
  const contentRef = useRef<HTMLDivElement>(null)
  /** Oyna yopilgach fokus qaytariladigan element. */
  const opener = useRef<HTMLElement | null>(null)
  const titleId = useId()
  const descriptionId = useId()

  // `open` false bo'lgach ham panel bir zumga qoladi — chiqish animatsiyasi
  const [mounted, setMounted] = useState(open)
  const [leaving, setLeaving] = useState(false)

  useEffect(() => {
    if (open) {
      setMounted(true)
      setLeaving(false)
      return
    }

    if (!mounted) return

    setLeaving(true)
    const timer = window.setTimeout(() => setMounted(false), CLOSE_MS)
    return () => window.clearTimeout(timer)
  }, [open, mounted])

  // Fon varaqlanmasin: modal ochiqligida orqadagi ro'yxat siljisa,
  // foydalanuvchi qayerda turganini yo'qotadi
  useEffect(() => {
    if (!mounted) return

    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previous
    }
  }, [mounted])

  /*
   * Fokus: ochilganda birinchi maydonga, yopilganda chaqirgan tugmaga.
   *
   * `mounted` ham bog'liqlikda: `open` true bo'lgan RENDER'da panel hali
   * DOM'da yo'q (u keyingi render'da paydo bo'ladi), shu sababli faqat
   * `open` ga qarab fokus qo'yilsa — qo'yadigan element topilmaydi.
   */
  useEffect(() => {
    if (!open || !mounted) return

    opener.current = document.activeElement as HTMLElement | null

    // Avval TANA ichidan qidiriladi: sarlavhadagi «yopish» tugmasi DOM'da
    // birinchi turadi, lekin oyna ochilganda kursor birinchi maydonda
    // bo'lishi kerak — foydalanuvchi darhol yozishni boshlaydi
    const panel = panelRef.current
    const first =
      contentRef.current?.querySelector<HTMLElement>(FOCUSABLE) ??
      panel?.querySelector<HTMLElement>(FOCUSABLE)
    ;(first ?? panel)?.focus()

    return () => {
      opener.current?.focus?.()
    }
  }, [open, mounted])

  /** Tab tugmasi panel ichida aylanadi — fon elementlariga o'tib ketmaydi. */
  const onKeyDown = useCallback(
    (event: ReactKeyboardEvent<HTMLDivElement>) => {
      if (event.key === 'Escape' && dismissible) {
        event.stopPropagation()
        onClose()
        return
      }

      if (event.key !== 'Tab') return

      const panel = panelRef.current
      if (!panel) return

      const items = [...panel.querySelectorAll<HTMLElement>(FOCUSABLE)]
      const first = items[0]
      const last = items.at(-1)
      if (!first || !last) return

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    },
    [dismissible, onClose],
  )

  if (!mounted) return null

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-6"
      onKeyDown={onKeyDown}
    >
      {/* Orqa fon: qoraytirish emas, bulantirish — iOS'da modal ostidagi
          kontent yo'qolmaydi, faqat fokusdan chiqadi.
          Tungi rejimda qoraytirish kuchliroq: deyarli qora fonda 25%
          qatlam umuman sezilmaydi va panel «osilib» qolgandek ko'rinadi. */}
      <button
        type="button"
        tabIndex={-1}
        aria-hidden
        onClick={dismissible ? onClose : undefined}
        className={cn(
          'absolute inset-0 cursor-default bg-black/25 backdrop-blur-[3px] dark:bg-black/55',
          'transition-opacity duration-200 ease-[var(--ease-ios)]',
          leaving ? 'opacity-0' : 'opacity-100',
        )}
      />

      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description === undefined ? undefined : descriptionId}
        tabIndex={-1}
        className={cn(
          'relative flex max-h-[92dvh] w-full flex-col outline-none',
          'rounded-t-2xl border border-[var(--border)] bg-[var(--bg-elevated)] shadow-floating',
          'sm:rounded-2xl',
          SIZES[size],
          leaving
            ? '[animation:pop-out_140ms_var(--ease-ios)]'
            : '[animation:pop-in_220ms_var(--ease-ios-out)]',
        )}
      >
        {/* Tor ekrandagi «tortish» chizig'i — varaq pastdan chiqqanini bildiradi */}
        <span
          aria-hidden
          className="mx-auto mt-2.5 h-1 w-9 shrink-0 rounded-full bg-[var(--border-strong)] sm:hidden"
        />

        <header className="flex items-start justify-between gap-4 px-6 pt-5 pb-4">
          <div className="min-w-0">
            <h2 id={titleId} className="text-title3 leading-tight font-semibold tracking-tight">
              {title}
            </h2>
            {description !== undefined && (
              <p id={descriptionId} className="mt-1.5 text-footnote text-[var(--fg-secondary)]">
                {description}
              </p>
            )}
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close')}
            title={t('common.close')}
            className={cn(
              'press -mt-1 -mr-1.5 grid size-9 shrink-0 place-items-center rounded-full',
              'bg-[var(--bg-sunken)] text-[var(--fg-secondary)]',
              'transition-colors hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
            )}
          >
            <X className="size-[18px]" strokeWidth={2.5} />
          </button>
        </header>

        <div ref={contentRef} className="min-h-0 flex-1 overflow-y-auto px-6 pb-6">
          {children}
        </div>

        {footer !== undefined && (
          <footer
            className={cn(
              'flex shrink-0 items-center justify-end gap-2.5 border-t border-[var(--border)]',
              'rounded-b-2xl bg-[var(--bg-sunken)] px-6 py-4',
            )}
          >
            {footer}
          </footer>
        )}
      </div>
    </div>,
    document.body,
  )
}
