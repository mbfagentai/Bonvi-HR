import { ChevronLeft, ChevronRight, Download, FileQuestion, LoaderCircle, X } from 'lucide-react'
import { Component, Suspense, lazy, useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import { ZoomableImage } from './zoomable-image'

/*
  PDF.js — faqat PDF ochilganda yuklanadi (~1 MB). Rasm ko'rish va qolgan sahifalar uni
  umuman tortmaydi.
*/
const PdfView = lazy(() => import('./pdf-view'))

/** Ko'rish oynasi nimani chiza oladi — tur MAZMUNDAN (server aniqlagan `contentType`). */
export type ViewerKind = 'image' | 'pdf' | 'other'

export function viewerKind(contentType: string | null | undefined): ViewerKind {
  if (contentType === null || contentType === undefined) return 'other'
  if (contentType.startsWith('image/')) return 'image'
  if (contentType === 'application/pdf') return 'pdf'
  return 'other'
}

/**
 * ⭐ To'liq ekranli ko'rish oynasi — hujjat va suratlarni YUKLAB OLMASDAN ko'rish (egasi,
 * 2026-10-30: «tortib olib keyin ochib ko'ramiz, vaqtni olyapti … faqat yengil ko'rish uchun
 * to'liq oyna»).
 *
 * Bitta qobiq hamma joyda: nomzod fayllari va anketalari, xodim hujjatlari va tekshiruv
 * fayllari, profil suratlari. Rasm — `ZoomableImage`, PDF — `PdfView`, boshqa tur —
 * «ko'rib bo'lmaydi, yuklab oling».
 *
 * <b>Oyna doim qorong'i</b> (iOS «Photos» ruhida): hujjat yorug' bo'ladi va uning atrofidagi
 * qorong'i maydon ko'zni varaqqa qaratadi. Shu sababli bu yerda mavzu tokenlari emas,
 * qat'iy oq/qora ishlatiladi.
 *
 * Klaviatura: Esc — yopish, ← / → — oldingi/keyingi fayl (ro'yxat berilgan bo'lsa),
 * «+», «−», «0» — kattalashtirish (ko'ruvchining o'zida).
 *
 * Baytlarni chaqiruvchi beradi (`blob`): har modulning o'z so'rovi va ruxsati bor, oyna esa
 * faqat chizadi. `blob === null` va xato yo'q — yuklanmoqda.
 */
export function FileViewer({
  open,
  onClose,
  title,
  subtitle,
  kind,
  blob,
  error = false,
  onDownload,
  downloading = false,
  position,
  onPrevious,
  onNext,
  actions,
}: {
  open: boolean
  onClose: () => void
  /** Odatda fayl nomi. */
  title: string
  /** Hajm, sana, tur — bir qator. */
  subtitle?: ReactNode
  kind: ViewerKind
  blob: Blob | null
  /** Baytlarni olib bo'lmadi — oyna xabar chizadi, yuklab olish tugmasi baribir ishlaydi. */
  error?: boolean
  onDownload?: () => void
  downloading?: boolean
  /** Ro'yxatdagi o'rni — «2 / 5». */
  position?: { index: number; total: number }
  onPrevious?: () => void
  onNext?: () => void
  /** Yuqori paneldagi qo'shimcha tugmalar (masalan, «Rasmni almashtirish»). */
  actions?: ReactNode
}) {
  const { t, num } = useLocale()
  const titleId = useId()
  const closeRef = useRef<HTMLButtonElement>(null)
  const opener = useRef<HTMLElement | null>(null)

  /*
    PDF.js `Blob` ning o'z turiga qaramaydi, `<img>` esa qaraydi — server turni har doim
    beradi, lekin oyna bunga tayanmaydi: rasm ko'rinishi uchun tur shart emas.
  */
  const src = useObjectUrl(kind === 'image' ? blob : null)

  // Varaqlash blokirovkasi, fokus va qaytarish — `Dialog` dagi bilan bir xil qoida
  useEffect(() => {
    if (!open) return

    opener.current = document.activeElement as HTMLElement | null
    closeRef.current?.focus()

    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    return () => {
      document.body.style.overflow = previous
      opener.current?.focus?.()
    }
  }, [open])

  useEffect(() => {
    if (!open) return

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation()
        onClose()
      } else if (event.key === 'ArrowLeft' && onPrevious !== undefined) {
        onPrevious()
      } else if (event.key === 'ArrowRight' && onNext !== undefined) {
        onNext()
      }
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose, onPrevious, onNext])

  if (!open) return null

  const navigable = position !== undefined && position.total > 1

  return createPortal(
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      className="fixed inset-0 z-[60] flex flex-col bg-neutral-950/95 text-white [animation:fade-in_160ms_var(--ease-ios-out)]"
    >
      {/* ───────── Yuqori panel ───────── */}
      <header className="flex shrink-0 items-center gap-2 px-3 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3 sm:px-5">
        <div className="min-w-0 flex-1">
          <h2 id={titleId} className="truncate text-callout font-semibold" title={title}>
            {title}
          </h2>
          {(subtitle !== undefined || navigable) && (
            <p className="tabular truncate text-caption1 text-white/60">
              {navigable && (
                <span className="font-semibold text-white/80">
                  {t('viewer.position', { n: num(position.index + 1), total: num(position.total) })}
                </span>
              )}
              {navigable && subtitle !== undefined && ' · '}
              {subtitle}
            </p>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-1">
          {actions}

          {onDownload !== undefined && (
            <TopButton label={t('documents.download')} onClick={onDownload} disabled={downloading}>
              {downloading ? <LoaderCircle className="size-5 animate-spin" /> : <Download className="size-5" />}
            </TopButton>
          )}

          <TopButton ref={closeRef} label={t('common.close')} onClick={onClose}>
            <X className="size-5" strokeWidth={2.5} />
          </TopButton>
        </div>
      </header>

      {/* ───────── Mazmun ───────── */}
      <div className="relative min-h-0 flex-1">
        {error ? (
          <Message icon={FileQuestion} text={t('viewer.loadFailed')} />
        ) : kind === 'other' ? (
          <Message icon={FileQuestion} text={t('viewer.unsupported')} />
        ) : blob === null ? (
          <div className="grid h-full place-items-center text-white/70">
            <LoaderCircle className="size-7 animate-spin" />
          </div>
        ) : kind === 'image' ? (
          src === null ? null : <ZoomableImage src={src} alt={title} />
        ) : (
          <ViewerBoundary key={title} fallback={<Message icon={FileQuestion} text={t('viewer.pdfFailed')} />}>
            <Suspense
              fallback={
                <div className="grid h-full place-items-center text-white/70">
                  <LoaderCircle className="size-7 animate-spin" />
                </div>
              }
            >
              <PdfView blob={blob} name={title} />
            </Suspense>
          </ViewerBoundary>
        )}

        {/* Oldingi / keyingi — chetlarda, kattalashtirish paneliga to'qnashmaydi */}
        {navigable && (
          <>
            <SideButton side="left" label={t('viewer.previous')} onClick={onPrevious} />
            <SideButton side="right" label={t('viewer.next')} onClick={onNext} />
          </>
        )}
      </div>
    </div>,
    document.body,
  )
}

/**
 * PDF ko'ruvchining xatosi oynadan chiqmasin: deploy'dan keyin ochiq qolgan tabda eski xeshli
 * chunk topilmasa yoki eski brauzer PDF.js ni ishga tushira olmasa, butun karta sahifasi
 * yiqilardi (qa-review 2026-10-30). Bu yerda faqat xabar — yuklab olish tugmasi ishlayveradi.
 */
class ViewerBoundary extends Component<{ fallback: ReactNode; children: ReactNode }, { failed: boolean }> {
  override state = { failed: false }

  static getDerivedStateFromError(): { failed: boolean } {
    return { failed: true }
  }

  override render() {
    return this.state.failed ? this.props.fallback : this.props.children
  }
}

function TopButton({
  ref,
  label,
  onClick,
  disabled = false,
  children,
}: {
  ref?: React.Ref<HTMLButtonElement>
  label: string
  onClick: () => void
  disabled?: boolean
  children: ReactNode
}) {
  return (
    <button
      ref={ref}
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      title={label}
      className="press grid size-10 place-items-center rounded-full bg-white/10 transition-colors hover:bg-white/20 disabled:opacity-50"
    >
      {children}
    </button>
  )
}

/** Yon tugma — mavjud bo'lmasa ko'rinmaydi (birinchi faylda «oldingi» yo'q). */
function SideButton({
  side,
  label,
  onClick,
}: {
  side: 'left' | 'right'
  label: string
  onClick: (() => void) | undefined
}) {
  if (onClick === undefined) return null
  const Icon = side === 'left' ? ChevronLeft : ChevronRight

  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      title={label}
      className={cn(
        'press absolute top-1/2 z-10 grid size-11 -translate-y-1/2 place-items-center rounded-full',
        'bg-black/60 text-white backdrop-blur-md transition-colors hover:bg-black/80',
        side === 'left' ? 'left-3' : 'right-3',
      )}
    >
      <Icon className="size-6" strokeWidth={2.5} />
    </button>
  )
}

function Message({ icon: Icon, text }: { icon: typeof FileQuestion; text: string }) {
  return (
    <div className="grid h-full place-items-center px-6">
      <div className="flex max-w-sm flex-col items-center gap-3 text-center">
        <Icon className="size-10 text-white/50" strokeWidth={1.5} />
        <p className="text-subhead text-white/80">{text}</p>
      </div>
    </div>
  )
}

/**
 * Ro'yxat ichida yurish: ochiq element, oldingi/keyingi va o'rni — barcha chaqiruvchilar
 * uchun bir xil hisob.
 */
export function useViewerList<T extends { id: string }>(items: readonly T[]) {
  const [openId, setOpenId] = useState<string | null>(null)

  const index = openId === null ? -1 : items.findIndex((item) => item.id === openId)
  const current = index === -1 ? null : (items[index] ?? null)
  const previous = index > 0 ? items[index - 1] : undefined
  const next = index !== -1 && index < items.length - 1 ? items[index + 1] : undefined

  return {
    current,
    open: (id: string) => setOpenId(id),
    close: () => setOpenId(null),
    position: index === -1 ? undefined : { index, total: items.length },
    onPrevious: previous === undefined ? undefined : () => setOpenId(previous.id),
    onNext: next === undefined ? undefined : () => setOpenId(next.id),
  }
}
