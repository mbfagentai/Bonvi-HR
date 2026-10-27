import { Eraser } from 'lucide-react'
import { useCallback, useEffect, useRef, type PointerEvent } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui'
import { canvasBlob } from '../lib/kiosk-images'
import { FieldMessage } from './kiosk-fields'

/** Width of the exported PNG — enough for the «Imzo» line of the PDF, far under 300 KB. */
const EXPORT_WIDTH = 900

/** CSS height of the pad — the `h-[220px]` class on the canvas. */
const HEIGHT = 220

type Point = { x: number; y: number }

/**
 * Finger, stylus or mouse signature. Strokes are kept as points relative to
 * the pad size, so rotating the device (which resizes the pad) redraws them
 * instead of wiping them. The PNG is exported with a transparent background after every stroke.
 */
export function SignaturePad({
  hasSignature,
  onChange,
  error,
}: {
  hasSignature: boolean
  onChange: (signature: Blob | null) => void
  error?: string | undefined
}) {
  const { t } = useLocale()
  const canvas = useRef<HTMLCanvasElement>(null)
  const strokes = useRef<Point[][]>([])
  const drawing = useRef(false)

  const paint = useCallback((target: HTMLCanvasElement, width: number, height: number, lineWidth: number) => {
    const context = target.getContext('2d')
    if (context === null) return

    context.clearRect(0, 0, target.width, target.height)
    context.lineCap = 'round'
    context.lineJoin = 'round'
    context.lineWidth = lineWidth
    // Dark ink regardless of theme: the PNG goes onto a white PDF page.
    context.strokeStyle = '#0B1F3A'

    for (const stroke of strokes.current) {
      const [first, ...rest] = stroke
      if (first === undefined) continue

      context.beginPath()
      context.moveTo(first.x * width, first.y * height)
      if (rest.length === 0) context.lineTo(first.x * width + 0.1, first.y * height)
      for (const point of rest) context.lineTo(point.x * width, point.y * height)
      context.stroke()
    }
  }, [])

  /** Repaints at the current backing size — cheap enough for every pointer move. */
  const redraw = useCallback(() => {
    const target = canvas.current
    if (target === null) return

    paint(target, target.width, target.height, 2.6 * (window.devicePixelRatio || 1))
  }, [paint])

  /** Matches the backing store to the CSS size (sharp on retina), then repaints. */
  const resize = useCallback(() => {
    const target = canvas.current
    if (target === null) return

    const ratio = window.devicePixelRatio || 1
    target.width = Math.round(target.clientWidth * ratio)
    target.height = Math.round(HEIGHT * ratio)
    redraw()
  }, [redraw])

  useEffect(() => {
    resize()
    const observer = new ResizeObserver(resize)
    if (canvas.current) observer.observe(canvas.current)
    return () => observer.disconnect()
  }, [resize])

  // The parent dropped the signature (a reset) — clear the pad too.
  useEffect(() => {
    if (!hasSignature && strokes.current.length > 0 && !drawing.current) {
      strokes.current = []
      redraw()
    }
  }, [hasSignature, redraw])

  const pointAt = (event: PointerEvent<HTMLCanvasElement>): Point => {
    const rect = event.currentTarget.getBoundingClientRect()
    return {
      x: Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width)),
      y: Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height)),
    }
  }

  const exportPng = async () => {
    const out = document.createElement('canvas')
    const target = canvas.current
    if (target === null) return

    const height = Math.round((EXPORT_WIDTH * HEIGHT) / Math.max(1, target.clientWidth))
    out.width = EXPORT_WIDTH
    out.height = height
    paint(out, out.width, out.height, 4)

    try {
      onChange(await canvasBlob(out, 'image/png'))
    } catch {
      onChange(null)
    }
  }

  const start = (event: PointerEvent<HTMLCanvasElement>) => {
    event.currentTarget.setPointerCapture(event.pointerId)
    drawing.current = true
    strokes.current.push([pointAt(event)])
    redraw()
  }

  const move = (event: PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return
    strokes.current.at(-1)?.push(pointAt(event))
    redraw()
  }

  const end = () => {
    if (!drawing.current) return
    drawing.current = false
    void exportPng()
  }

  const clear = () => {
    strokes.current = []
    redraw()
    onChange(null)
  }

  return (
    <div data-field="signature" className="space-y-2.5">
      <div
        className={cn(
          'relative overflow-hidden rounded-xl border-2 bg-white',
          error ? 'border-ios-red/60' : 'border-[var(--border-strong)]',
        )}
      >
        <canvas
          ref={canvas}
          aria-label={t('kiosk.signature.pad')}
          // `h-[220px]` must stay equal to `HEIGHT` (the backing store is sized from it).
          className="block h-[220px] w-full touch-none"
          onPointerDown={start}
          onPointerMove={move}
          onPointerUp={end}
          onPointerCancel={end}
        />

        {/* The signing line, like on the paper. */}
        <span
          aria-hidden
          className="pointer-events-none absolute inset-x-8 bottom-12 border-b border-dashed border-slate-300"
        />
        {!hasSignature && (
          <span className="pointer-events-none absolute inset-x-0 bottom-4 text-center text-footnote text-slate-400">
            {t('kiosk.signature.placeholder')}
          </span>
        )}
      </div>

      <div className="flex items-center justify-between gap-3">
        <FieldMessage error={error} />
        <Button type="button" variant="secondary" size="lg" pill onClick={clear} disabled={!hasSignature} className="ml-auto">
          <Eraser />
          {t('kiosk.signature.clear')}
        </Button>
      </div>
    </div>
  )
}
