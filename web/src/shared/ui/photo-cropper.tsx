import { Minus, Plus } from 'lucide-react'
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type KeyboardEvent,
  type PointerEvent,
  type SyntheticEvent,
} from 'react'

import type { CropArea } from '@/shared/lib/photo-crop'
import { cn } from '@/shared/lib/cn'

/** Ko'rish oynasining tomoni (px) — `size-64`. */
const VIEW = 256
const MAX_ZOOM = 4

interface Natural {
  width: number
  height: number
}

interface View {
  zoom: number
  /** Rasmning chap-yuqori burchagi ko'rish oynasiga nisbatan (px). */
  x: number
  y: number
}

/**
 * Suratni kvadratga joylashtirish — surish va kattalashtirish.
 *
 * Kvadrat oyna — saqlanadigan qism (serverda 512×512); doira chizig'i esa
 * kartadagi yumaloq avatar qayerni ko'rsatishini oldindan aytadi. Rasm
 * har doim oynani TO'LIQ qoplaydi: bo'sh joy qolmaydi, ya'ni saqlangan
 * surat chetida oq yoki qora tasma bo'lmaydi.
 *
 * Boshqaruv: sichqoncha yoki barmoq bilan surish, g'ildirak va pastdagi
 * surgich bilan kattalashtirish, klaviaturada strelkalar va «+» / «−».
 *
 * ⚠️ Yangi rasm uchun chaqiruvchi `key={src}` beradi — komponent noldan
 * yaratiladi. Holatni `src` o'zgarishida effekt bilan tozalash xavfli: keshdan
 * tez yuklangan rasmning `onLoad` i effektdan OLDIN kelsa, tozalash uni
 * bekor qilib, oyna bo'sh qolardi.
 */
export function PhotoCropper({
  src,
  onChange,
  zoomLabel,
  hint,
  className,
}: {
  src: string
  onChange: (area: CropArea | null) => void
  zoomLabel: string
  hint?: string
  className?: string
}) {
  const [natural, setNatural] = useState<Natural | null>(null)
  const [view, setView] = useState<View>({ zoom: 1, x: 0, y: 0 })
  const drag = useRef<{ id: number; startX: number; startY: number; fromX: number; fromY: number } | null>(null)
  const frame = useRef<HTMLDivElement>(null)
  const zoomRef = useRef<(factor: number) => void>(() => {})

  const baseScale = natural === null ? 1 : VIEW / Math.min(natural.width, natural.height)

  /** Rasm oynadan chiqib ketmasin — har doim to'liq qoplab tursin. */
  const clamp = useCallback(
    (next: View): View => {
      if (natural === null) return next
      const scale = baseScale * next.zoom
      const width = natural.width * scale
      const height = natural.height * scale
      return {
        zoom: next.zoom,
        x: Math.min(0, Math.max(VIEW - width, next.x)),
        y: Math.min(0, Math.max(VIEW - height, next.y)),
      }
    },
    [natural, baseScale],
  )

  const onLoad = (event: SyntheticEvent<HTMLImageElement>) => {
    const image = event.currentTarget
    const size = { width: image.naturalWidth, height: image.naturalHeight }
    const scale = VIEW / Math.min(size.width, size.height)
    setNatural(size)
    setView({
      zoom: 1,
      x: (VIEW - size.width * scale) / 2,
      // Bo'yi baland rasmda bosh odatda yuqorida — boshlang'ich oyna markazga emas, yuqoriroqqa.
      y: size.height > size.width ? -(size.height * scale - VIEW) / 6 : (VIEW - size.height * scale) / 2,
    })
  }

  // Saqlanadigan bo'lak har o'zgarishda chaqiruvchiga — asl piksellarda.
  useEffect(() => {
    if (natural === null) return
    const scale = baseScale * view.zoom
    onChange({ x: -view.x / scale, y: -view.y / scale, size: VIEW / scale })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [natural, view, baseScale])

  /** Kattalashtirish — oyna markazi joyida qoladi. */
  const zoomTo = (zoom: number) => {
    const next = Math.min(MAX_ZOOM, Math.max(1, zoom))
    setView((current) => {
      const ratio = next / current.zoom
      const center = VIEW / 2
      return clamp({ zoom: next, x: center - (center - current.x) * ratio, y: center - (center - current.y) * ratio })
    })
  }

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    event.currentTarget.setPointerCapture(event.pointerId)
    drag.current = { id: event.pointerId, startX: event.clientX, startY: event.clientY, fromX: view.x, fromY: view.y }
  }

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const start = drag.current
    if (start === null || start.id !== event.pointerId) return
    setView((current) => clamp({ ...current, x: start.fromX + event.clientX - start.startX, y: start.fromY + event.clientY - start.startY }))
  }

  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    if (drag.current?.id === event.pointerId) drag.current = null
  }

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const step = event.shiftKey ? 40 : 10
    const moves: Record<string, [number, number]> = {
      ArrowLeft: [step, 0],
      ArrowRight: [-step, 0],
      ArrowUp: [0, step],
      ArrowDown: [0, -step],
    }
    const move = moves[event.key]
    if (move !== undefined) {
      event.preventDefault()
      const [dx, dy] = move
      setView((current) => clamp({ ...current, x: current.x + dx, y: current.y + dy }))
    } else if (event.key === '+' || event.key === '=') {
      event.preventDefault()
      zoomTo(view.zoom + 0.2)
    } else if (event.key === '-') {
      event.preventDefault()
      zoomTo(view.zoom - 0.2)
    }
  }

  // Tinglovchi bir marta ulanadi, eng so'nggi holatni esa shu havola orqali oladi.
  useEffect(() => {
    zoomRef.current = (factor: number) => zoomTo(view.zoom * factor)
  })

  // G'ildirak — o'zimizning (passiv emas) tinglovchi: aks holda kattalashtirish bilan birga
  // oyna orqasidagi sahifa ham aylanib ketardi.
  useEffect(() => {
    const element = frame.current
    if (element === null) return
    const onWheel = (event: WheelEvent) => {
      event.preventDefault()
      zoomRef.current(event.deltaY < 0 ? 1.1 : 1 / 1.1)
    }
    element.addEventListener('wheel', onWheel, { passive: false })
    return () => element.removeEventListener('wheel', onWheel)
  }, [])

  const scale = baseScale * view.zoom

  return (
    <div className={cn('flex flex-col items-center gap-3', className)}>
      <div
        ref={frame}
        role="application"
        aria-label={hint}
        tabIndex={0}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerUp}
        onKeyDown={onKeyDown}
        className={cn(
          'relative size-64 touch-none overflow-hidden rounded-lg bg-[var(--bg-sunken)] select-none',
          'cursor-grab active:cursor-grabbing',
          'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--accent)]',
        )}
      >
        <img
          src={src}
          alt=""
          draggable={false}
          onLoad={onLoad}
          className="pointer-events-none absolute top-0 left-0 max-w-none origin-top-left"
          style={
            natural === null
              ? { visibility: 'hidden' }
              : { width: natural.width * scale, height: natural.height * scale, transform: `translate(${view.x}px, ${view.y}px)` }
          }
        />
        {/* Kartadagi yumaloq avatar chegarasi — kvadratning o'zi saqlanadi */}
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 rounded-full shadow-[0_0_0_9999px_rgb(0_0_0/0.35)] ring-2 ring-white/80"
        />
      </div>

      <label className="flex w-64 items-center gap-2 text-[var(--fg-secondary)]">
        <Minus className="size-4 shrink-0" aria-hidden />
        <input
          type="range"
          min={1}
          max={MAX_ZOOM}
          step={0.01}
          value={view.zoom}
          onChange={(event) => zoomTo(Number(event.target.value))}
          aria-label={zoomLabel}
          className="w-full accent-[var(--accent)]"
        />
        <Plus className="size-4 shrink-0" aria-hidden />
      </label>

      {hint !== undefined && <p className="max-w-64 text-center text-footnote text-[var(--fg-tertiary)]">{hint}</p>}
    </div>
  )
}
