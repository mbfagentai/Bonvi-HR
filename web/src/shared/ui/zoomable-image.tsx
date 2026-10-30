import { useCallback, useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'

import { cn } from '@/shared/lib/cn'
import { VIEWER_ZOOM_STEP, ViewerZoomBar, clampZoom } from './viewer-zoom-bar'

interface View {
  zoom: number
  x: number
  y: number
}

const FIT: View = { zoom: 1, x: 0, y: 0 }

/** Ikki marta bosilganda shu darajaga yaqinlashadi — pasport sahifasidagi matn o'qiladigan daraja. */
const DOUBLE_TAP_ZOOM = 2.5

/**
 * Kattalashtiriladigan rasm — ko'rish oynasining (`FileViewer`) ichida.
 *
 * Boshqaruv qurilmaning o'z imo-ishoralari bilan (egasi, 2026-10-30: «yanada kattalashtirib
 * ko'rish mumkin bo'lsin»):
 *   · sichqoncha g'ildiragi / trackpad — kursor turgan nuqta atrofida;
 *   · ikki barmoq bilan chimchilash — barmoqlar orasidagi nuqta atrofida;
 *   · ikki marta bosish — shu nuqtaga 250%, yana ikki marta — sig'dirishga;
 *   · kattalashganda sudrab surish;
 *   · pastdagi panel va klaviatura: «+», «−», «0».
 *
 * Rasm boshida ekranga SIG'ADI (100%) va undan kichraymaydi: kichraytirishning bu yerda
 * ma'nosi yo'q, faqat chetga qochib ketgan rasmni izlashga majbur qilardi. Surish ham
 * chegaralangan — rasm chetidan nariga ketmaydi.
 *
 * ⚠️ Saqlangan profil surati 512×512 (`PhotoLimits.SquareSize`): katta ekranda 300–400% dan
 * keyin piksellar ko'rina boshlaydi. Bu ko'ruvchining emas, faylning chegarasi.
 */
export function ZoomableImage({ src, alt, className }: { src: string; alt: string; className?: string }) {
  const frameRef = useRef<HTMLDivElement>(null)
  const imageRef = useRef<HTMLImageElement>(null)
  const [view, setView] = useState<View>(FIT)
  /** Barmoq/sichqoncha bosib turilibdi — o'tish animatsiyasi o'chadi. */
  const [gesturing, setGesturing] = useState(false)

  /** Faol barmoqlar/sichqoncha — chimchilash uchun ikkitasi kerak. */
  const pointers = useRef(new Map<number, { x: number; y: number }>())
  const gesture = useRef<{ distance: number; zoom: number; x: number; y: number; midX: number; midY: number } | null>(null)
  const lastTap = useRef<{ at: number; x: number; y: number } | null>(null)

  // Boshqa rasm — sig'dirilgan holatdan
  useEffect(() => setView(FIT), [src])

  /** Rasm ramka chetidan nariga ketmasin; sig'ib turgan o'qda — markazda. */
  const clampView = useCallback((next: View): View => {
    const frame = frameRef.current
    const image = imageRef.current
    if (frame === null || image === null) return next

    const maxX = Math.max(0, (image.offsetWidth * next.zoom - frame.clientWidth) / 2)
    const maxY = Math.max(0, (image.offsetHeight * next.zoom - frame.clientHeight) / 2)

    return {
      zoom: next.zoom,
      x: Math.min(maxX, Math.max(-maxX, next.x)),
      y: Math.min(maxY, Math.max(-maxY, next.y)),
    }
  }, [])

  /**
   * Nuqta atrofida kattalashtirish: `(px, py)` — ramka MARKAZIGA nisbatan. Shu nuqta ostidagi
   * piksel joyida qoladi, qolgani undan uzoqlashadi — xaritalardagi kabi.
   */
  const zoomAt = useCallback(
    (target: number, px: number, py: number) => {
      setView((current) => {
        const zoom = clampZoom(target)
        const ratio = zoom / current.zoom
        return clampView({ zoom, x: px - (px - current.x) * ratio, y: py - (py - current.y) * ratio })
      })
    },
    [clampView],
  )

  /** Ramka markaziga nisbatan koordinata. */
  const fromCenter = useCallback((clientX: number, clientY: number) => {
    const rect = frameRef.current?.getBoundingClientRect()
    if (rect === undefined) return { x: 0, y: 0 }
    return { x: clientX - rect.left - rect.width / 2, y: clientY - rect.top - rect.height / 2 }
  }, [])

  /*
    G'ildirak — passiv EMAS tinglovchi: `preventDefault` sahifani aylantirib yubormasin.
    React'ning `onWheel` i passiv ulanadi va unda buni qilib bo'lmaydi.
  */
  useEffect(() => {
    const frame = frameRef.current
    if (frame === null) return

    const onWheel = (event: WheelEvent) => {
      event.preventDefault()
      const point = fromCenter(event.clientX, event.clientY)
      // Trackpad chimchilashi `ctrlKey` bilan keladi va qadami mayda — sezgirroq
      const speed = event.ctrlKey ? 0.01 : 0.0025
      setView((current) => {
        const zoom = clampZoom(current.zoom * Math.exp(-event.deltaY * speed))
        const ratio = zoom / current.zoom
        return clampView({ zoom, x: point.x - (point.x - current.x) * ratio, y: point.y - (point.y - current.y) * ratio })
      })
    }

    frame.addEventListener('wheel', onWheel, { passive: false })
    return () => frame.removeEventListener('wheel', onWheel)
  }, [clampView, fromCenter])

  // Klaviatura: «+», «−», «0» — ramka markazi atrofida
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.metaKey || event.ctrlKey || event.altKey) return
      if (event.key === '+' || event.key === '=') zoomAt(view.zoom * VIEWER_ZOOM_STEP, 0, 0)
      else if (event.key === '-' || event.key === '_') zoomAt(view.zoom / VIEWER_ZOOM_STEP, 0, 0)
      else if (event.key === '0') setView(FIT)
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [view.zoom, zoomAt])

  // Oyna o'lchami o'zgarsa (telefonni burish) — chegaralar qayta hisoblanadi
  useEffect(() => {
    const onResize = () => setView((current) => clampView(current))
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [clampView])

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    event.currentTarget.setPointerCapture(event.pointerId)
    pointers.current.set(event.pointerId, { x: event.clientX, y: event.clientY })
    setGesturing(true)

    if (pointers.current.size === 2) {
      const [a, b] = [...pointers.current.values()] as [{ x: number; y: number }, { x: number; y: number }]
      const mid = fromCenter((a.x + b.x) / 2, (a.y + b.y) / 2)
      gesture.current = {
        distance: Math.hypot(a.x - b.x, a.y - b.y),
        zoom: view.zoom,
        x: view.x,
        y: view.y,
        midX: mid.x,
        midY: mid.y,
      }
      return
    }

    // Ikki marta bosish (sichqoncha ham, barmoq ham) — 300 ms va 30 px ichida
    const now = performance.now()
    const previous = lastTap.current
    if (previous !== null && now - previous.at < 300 && Math.hypot(event.clientX - previous.x, event.clientY - previous.y) < 30) {
      lastTap.current = null
      if (view.zoom > 1.01) setView(FIT)
      else {
        const point = fromCenter(event.clientX, event.clientY)
        zoomAt(DOUBLE_TAP_ZOOM, point.x, point.y)
      }
      return
    }
    lastTap.current = { at: now, x: event.clientX, y: event.clientY }
  }

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const previous = pointers.current.get(event.pointerId)
    if (previous === undefined) return
    pointers.current.set(event.pointerId, { x: event.clientX, y: event.clientY })

    const pinch = gesture.current
    if (pinch !== null && pointers.current.size >= 2) {
      const [a, b] = [...pointers.current.values()] as [{ x: number; y: number }, { x: number; y: number }]
      const zoom = clampZoom(pinch.zoom * (Math.hypot(a.x - b.x, a.y - b.y) / Math.max(pinch.distance, 1)))
      const mid = fromCenter((a.x + b.x) / 2, (a.y + b.y) / 2)
      const ratio = zoom / pinch.zoom
      // Barmoqlar orasidagi nuqta joyida qoladi va barmoqlar bilan birga suriladi
      setView(
        clampView({
          zoom,
          x: mid.x - (pinch.midX - pinch.x) * ratio,
          y: mid.y - (pinch.midY - pinch.y) * ratio,
        }),
      )
      return
    }

    if (view.zoom <= 1.001) return
    setView((current) =>
      clampView({ zoom: current.zoom, x: current.x + event.clientX - previous.x, y: current.y + event.clientY - previous.y }),
    )
  }

  const onPointerEnd = (event: ReactPointerEvent<HTMLDivElement>) => {
    pointers.current.delete(event.pointerId)
    if (pointers.current.size < 2) gesture.current = null
    if (pointers.current.size === 0) setGesturing(false)
  }

  const zoomed = view.zoom > 1.001

  return (
    <div className={cn('relative h-full w-full', className)}>
      <div
        ref={frameRef}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerEnd}
        onPointerCancel={onPointerEnd}
        // Brauzerning o'z chimchilashi va aylantirishi o'chadi — imo-ishoralar shu yerda
        className={cn(
          'absolute inset-0 grid touch-none place-items-center overflow-hidden select-none',
          zoomed ? 'cursor-grab active:cursor-grabbing' : 'cursor-zoom-in',
        )}
      >
        <img
          ref={imageRef}
          src={src}
          alt={alt}
          draggable={false}
          className="max-h-full max-w-full object-contain will-change-transform"
          style={{
            transform: `translate3d(${view.x}px, ${view.y}px, 0) scale(${view.zoom})`,
            // Barmoq/sichqoncha ortidan darhol; tugma va ikki marta bosishda — silliq
            transition: gesturing ? 'none' : 'transform 160ms var(--ease-ios-out)',
          }}
        />
      </div>

      <div className="pointer-events-none absolute inset-x-0 bottom-4 flex justify-center">
        <ViewerZoomBar
          zoom={view.zoom}
          onZoomIn={() => zoomAt(view.zoom * VIEWER_ZOOM_STEP, 0, 0)}
          onZoomOut={() => zoomAt(view.zoom / VIEWER_ZOOM_STEP, 0, 0)}
          onReset={() => setView(FIT)}
        />
      </div>
    </div>
  )
}
