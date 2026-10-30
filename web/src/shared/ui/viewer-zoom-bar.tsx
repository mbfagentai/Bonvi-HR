import { Maximize, Minus, Plus } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

/** Kattalashtirish chegaralari — rasm ham, PDF ham bir xil qadam bilan yuradi. */
export const VIEWER_MIN_ZOOM = 1
export const VIEWER_MAX_ZOOM = 8
export const VIEWER_ZOOM_STEP = 1.5

export function clampZoom(value: number, min = VIEWER_MIN_ZOOM, max = VIEWER_MAX_ZOOM): number {
  return Math.min(max, Math.max(min, value))
}

/**
 * Ko'rish oynasining pastidagi kattalashtirish paneli: «−», foiz, «+», «Sig'dirish».
 *
 * Rasm va PDF uchun BITTA panel (`FileViewer`): HR ikki xil hujjat uchun ikki xil boshqaruvni
 * o'rganmasin. Foiz — «sig'dirilgan» holatga nisbatan (100% = ekranga to'liq sig'gan), asl
 * piksellarga emas: HR uchun savol «qanchalik yaqinlashtirdim», fayl o'lchami emas.
 *
 * Panel fon ustida suzadi va qorong'i: ko'rish oynasining o'zi doim qorong'i (iOS «Photos»
 * ruhida) — kunduzgi mavzuda ham hujjat atrofidagi oq maydon ko'zni olmasin.
 */
export function ViewerZoomBar({
  zoom,
  onZoomIn,
  onZoomOut,
  onReset,
  min = VIEWER_MIN_ZOOM,
  max = VIEWER_MAX_ZOOM,
  className,
}: {
  zoom: number
  onZoomIn: () => void
  onZoomOut: () => void
  onReset: () => void
  min?: number
  max?: number
  className?: string
}) {
  const { t, num } = useLocale()

  return (
    <div
      className={cn(
        'pointer-events-auto flex items-center gap-1 rounded-full bg-black/70 p-1 text-white shadow-floating backdrop-blur-md',
        className,
      )}
    >
      <ZoomButton label={t('viewer.zoomOut')} onClick={onZoomOut} disabled={zoom <= min + 0.001}>
        <Minus className="size-4" strokeWidth={2.5} />
      </ZoomButton>

      {/* Foizni bosish — sig'dirishga qaytish: eng ko'p kerak bo'ladigan «orqaga» shu */}
      <button
        type="button"
        onClick={onReset}
        title={t('viewer.fit')}
        className="press tabular min-w-14 rounded-full px-2 py-1.5 text-center text-footnote font-semibold hover:bg-white/15"
      >
        {num(Math.round(zoom * 100))}%
      </button>

      <ZoomButton label={t('viewer.zoomIn')} onClick={onZoomIn} disabled={zoom >= max - 0.001}>
        <Plus className="size-4" strokeWidth={2.5} />
      </ZoomButton>

      <span aria-hidden className="mx-0.5 h-5 w-px bg-white/25" />

      <ZoomButton label={t('viewer.fit')} onClick={onReset} disabled={Math.abs(zoom - 1) < 0.001}>
        <Maximize className="size-4" strokeWidth={2.5} />
      </ZoomButton>
    </div>
  )
}

function ZoomButton({
  label,
  onClick,
  disabled,
  children,
}: {
  label: string
  onClick: () => void
  disabled: boolean
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      title={label}
      className="press grid size-9 place-items-center rounded-full hover:bg-white/15 disabled:opacity-35 disabled:hover:bg-transparent"
    >
      {children}
    </button>
  )
}
