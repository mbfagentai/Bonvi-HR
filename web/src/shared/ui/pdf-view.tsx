import { LoaderCircle } from 'lucide-react'
import { GlobalWorkerOptions, getDocument, type PDFDocumentProxy, type RenderTask } from 'pdfjs-dist'
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url'
import { useCallback, useEffect, useLayoutEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { VIEWER_ZOOM_STEP, ViewerZoomBar, clampZoom } from './viewer-zoom-bar'

/*
  Ishchi (worker) — o'z faylimizdan, CDN'dan emas: CSP `script-src 'self'` va tizim tashqi
  serverga bog'lanmasin. Vite uni xeshli asset qilib chiqaradi (uzoq keshlanadi).
*/
GlobalWorkerOptions.workerSrc = workerUrl

/** Sig'dirilgan sahifaning eng katta eni — keng monitorda A4 varaq bo'yi ekrandan oshib ketmasin. */
const FIT_MAX_WIDTH = 960

/** Sahifalar orasidagi va chetdagi bo'shliq (px). */
const GAP = 16

/** Canvas aniqligining yuqori chegarasi: 3× retina'da 8× kattalashtirish xotirani to'ldirardi. */
const MAX_PIXEL_RATIO = 2

/** PDF.js canvas'ining eng katta tomoni — brauzerlar bundan kattasini oq chizadi. */
const MAX_CANVAS_SIDE = 8192

/**
 * Bitta canvas'ning eng ko'p piksellari (~16 Mpx ≈ 64 MB). iPad/iPhone Safari canvas xotirasini
 * bir necha yuz MB bilan cheklaydi: chegarasiz 800% da bitta sahifa ~190 MB olardi va varaqlar
 * oq bo'lib, keyin tab yiqilardi (qa-review 2026-10-30). Undan kattasi biroz xira, lekin ko'rinadi.
 */
const MAX_CANVAS_PIXELS = 16_000_000

interface PageSize {
  width: number
  height: number
}

/**
 * PDF ko'ruvchi — PDF.js bilan, ko'rish oynasining ichida (`FileViewer`, egasi 2026-10-30:
 * «yuklab olib ochib ko'ramiz, vaqt oladi»).
 *
 * <b>Nega brauzerning o'z ko'ruvchisi (`<iframe>`) emas.</b> Ikki sabab: (1) prod CSP
 * `frame-src 'self'` va `object-src 'none'` — `blob:` iframe'ni to'sadi va PDF modulini
 * o'chiradi; (2) Android Chrome va ko'p telefon brauzerlari iframe ichida PDF'ni umuman
 * chizmaydi. PDF.js har joyda bir xil ishlaydi va boshqaruv rasmniki bilan bir xil
 * (`ViewerZoomBar`).
 *
 * Sahifalar KO'RINGANDA chiziladi (`IntersectionObserver`): 40 betlik skaner hammasi
 * birdaniga chizilsa, telefon qotib qolardi. Kattalashtirish sahifani qayta chizadi —
 * matn xira bo'lmasin; chimchilash davomida esa faqat CSS bilan cho'ziladi va barmoq
 * qo'yilgach bir marta chiziladi.
 */
export default function PdfView({ blob, name }: { blob: Blob; name: string }) {
  const { t, num } = useLocale()
  const scrollRef = useRef<HTMLDivElement>(null)

  const [doc, setDoc] = useState<PDFDocumentProxy | null>(null)
  const [sizes, setSizes] = useState<PageSize[]>([])
  const [failed, setFailed] = useState(false)
  const [frameWidth, setFrameWidth] = useState(0)
  const [zoom, setZoom] = useState(1)
  const [pinchScale, setPinchScale] = useState(1)
  const [currentPage, setCurrentPage] = useState(1)

  // ── Hujjatni ochish ──
  useEffect(() => {
    let cancelled = false
    setDoc(null)
    setSizes([])
    setFailed(false)
    setZoom(1)

    let loading: ReturnType<typeof getDocument> | null = null

    void blob
      .arrayBuffer()
      .then(async (buffer) => {
        if (cancelled) return
        loading = getDocument({ data: new Uint8Array(buffer) })
        const opened = await loading.promise

        // Har sahifaning o'lchami oldindan — joy egallovchilar to'g'ri bo'yda, aylantirish sakramasin
        const pages = await Promise.all(
          Array.from({ length: opened.numPages }, (_, index) =>
            opened.getPage(index + 1).then((page) => {
              const viewport = page.getViewport({ scale: 1 })
              return { width: viewport.width, height: viewport.height }
            }),
          ),
        )

        if (cancelled) {
          void opened.destroy()
          return
        }

        setSizes(pages)
        setDoc(opened)
      })
      .catch(() => {
        if (!cancelled) setFailed(true)
      })

    return () => {
      cancelled = true
      void loading?.destroy()
    }
  }, [blob])

  // ── Ramka eni — sig'dirish shundan ──
  useEffect(() => {
    const frame = scrollRef.current
    if (frame === null) return

    const observer = new ResizeObserver(([entry]) => {
      if (entry !== undefined) setFrameWidth(entry.contentRect.width)
    })
    observer.observe(frame)
    return () => observer.disconnect()
  }, [])

  const fitWidth = Math.max(Math.min(frameWidth - GAP * 2, FIT_MAX_WIDTH), 120)
  const pageWidth = fitWidth * zoom

  // ── Kattalashtirish: ko'rinib turgan nuqta joyida qolsin ──
  const anchor = useRef<{ x: number; y: number } | null>(null)

  const changeZoom = useCallback((next: number) => {
    const frame = scrollRef.current
    if (frame !== null && frame.scrollHeight > 0) {
      anchor.current = {
        x: (frame.scrollLeft + frame.clientWidth / 2) / frame.scrollWidth,
        y: (frame.scrollTop + frame.clientHeight / 2) / frame.scrollHeight,
      }
    }
    setZoom(clampZoom(next))
  }, [])

  useLayoutEffect(() => {
    const frame = scrollRef.current
    const point = anchor.current
    if (frame === null || point === null) return
    anchor.current = null
    frame.scrollLeft = point.x * frame.scrollWidth - frame.clientWidth / 2
    frame.scrollTop = point.y * frame.scrollHeight - frame.clientHeight / 2
  }, [zoom])

  // Ctrl + g'ildirak (trackpad chimchilashi ham shunday keladi) — kattalashtirish; oddiy g'ildirak — aylantirish
  useEffect(() => {
    const frame = scrollRef.current
    if (frame === null) return

    const onWheel = (event: WheelEvent) => {
      if (!event.ctrlKey && !event.metaKey) return
      event.preventDefault()
      setZoom((current) => {
        const next = clampZoom(current * Math.exp(-event.deltaY * 0.01))
        anchor.current = {
          x: (frame.scrollLeft + frame.clientWidth / 2) / frame.scrollWidth,
          y: (frame.scrollTop + frame.clientHeight / 2) / frame.scrollHeight,
        }
        return next
      })
    }

    frame.addEventListener('wheel', onWheel, { passive: false })
    return () => frame.removeEventListener('wheel', onWheel)
  }, [])

  // Klaviatura: «+», «−», «0»
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.metaKey || event.ctrlKey || event.altKey) return
      if (event.key === '+' || event.key === '=') changeZoom(zoom * VIEWER_ZOOM_STEP)
      else if (event.key === '-' || event.key === '_') changeZoom(zoom / VIEWER_ZOOM_STEP)
      else if (event.key === '0') changeZoom(1)
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [zoom, changeZoom])

  // ── Ikki barmoq bilan chimchilash (telefon, planshet) ──
  const touches = useRef(new Map<number, { x: number; y: number }>())
  const pinchStart = useRef<number | null>(null)

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.pointerType !== 'touch') return
    touches.current.set(event.pointerId, { x: event.clientX, y: event.clientY })
    if (touches.current.size === 2) {
      const [a, b] = [...touches.current.values()] as [{ x: number; y: number }, { x: number; y: number }]
      pinchStart.current = Math.hypot(a.x - b.x, a.y - b.y)
    }
  }

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!touches.current.has(event.pointerId)) return
    touches.current.set(event.pointerId, { x: event.clientX, y: event.clientY })
    const start = pinchStart.current
    if (start === null || touches.current.size < 2) return
    const [a, b] = [...touches.current.values()] as [{ x: number; y: number }, { x: number; y: number }]
    const scale = Math.hypot(a.x - b.x, a.y - b.y) / Math.max(start, 1)
    setPinchScale(clampZoom(zoom * scale) / zoom)
  }

  const onPointerEnd = (event: ReactPointerEvent<HTMLDivElement>) => {
    touches.current.delete(event.pointerId)
    if (touches.current.size < 2 && pinchStart.current !== null) {
      pinchStart.current = null
      if (Math.abs(pinchScale - 1) > 0.01) changeZoom(zoom * pinchScale)
      setPinchScale(1)
    }
  }

  // ── Joriy sahifa — ekran o'rtasidagi sahifa ──
  const onScroll = () => {
    const frame = scrollRef.current
    if (frame === null) return
    const middle = frame.scrollTop + frame.clientHeight / 2
    const pages = frame.querySelectorAll<HTMLElement>('[data-pdf-page]')
    let found = 1
    for (const page of pages) {
      if (page.offsetTop <= middle) found = Number(page.dataset['pdfPage'])
      else break
    }
    setCurrentPage(found)
  }

  return (
    <div className="relative h-full w-full">
      <div
        ref={scrollRef}
        onScroll={onScroll}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerEnd}
        onPointerCancel={onPointerEnd}
        // Bir barmoq — tabiiy aylantirish; ikki barmoq — bizning chimchilash
        className="absolute inset-0 touch-pan-x touch-pan-y overflow-auto overscroll-contain"
      >
        {failed ? (
          <div className="grid h-full place-items-center px-6 text-center text-subhead text-white/80">
            {t('viewer.pdfFailed')}
          </div>
        ) : doc === null || frameWidth === 0 ? (
          <div className="grid h-full place-items-center text-white/70">
            <LoaderCircle className="size-7 animate-spin" />
          </div>
        ) : (
          <div
            className="mx-auto flex w-max min-w-full flex-col items-center"
            style={{
              gap: GAP,
              padding: `${GAP}px ${GAP}px 96px`,
              transform: pinchScale === 1 ? undefined : `scale(${pinchScale})`,
              transformOrigin: 'top center',
            }}
          >
            {sizes.map((size, index) => (
              <PdfPage
                key={index}
                doc={doc}
                number={index + 1}
                width={pageWidth}
                height={(pageWidth * size.height) / size.width}
                label={`${name} — ${index + 1}`}
              />
            ))}
          </div>
        )}
      </div>

      {doc !== null && sizes.length > 1 && (
        <div className="pointer-events-none absolute inset-x-0 top-3 flex justify-center">
          <span className="tabular rounded-full bg-black/70 px-3 py-1 text-caption1 font-semibold text-white backdrop-blur-md">
            {t('viewer.pagePosition', { n: num(currentPage), total: num(sizes.length) })}
          </span>
        </div>
      )}

      {doc !== null && (
        <div className="pointer-events-none absolute inset-x-0 bottom-4 flex justify-center">
          <ViewerZoomBar
            zoom={zoom * pinchScale}
            onZoomIn={() => changeZoom(zoom * VIEWER_ZOOM_STEP)}
            onZoomOut={() => changeZoom(zoom / VIEWER_ZOOM_STEP)}
            onReset={() => changeZoom(1)}
          />
        </div>
      )}
    </div>
  )
}

/**
 * Bitta sahifa: joy egallovchi to'g'ri o'lchamda doim turadi, canvas esa faqat ko'rinish
 * yaqinida chiziladi va kenglik o'zgarsa qayta chiziladi (oldingi chizish bekor qilinadi).
 */
function PdfPage({
  doc,
  number,
  width,
  height,
  label,
}: {
  doc: PDFDocumentProxy
  number: number
  width: number
  height: number
  label: string
}) {
  const boxRef = useRef<HTMLDivElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [visible, setVisible] = useState(false)
  const [rendered, setRendered] = useState(false)

  useEffect(() => {
    const box = boxRef.current
    if (box === null) return

    // Ekrandan bir-ikki sahifa oldin — aylantirganda oq varaq ko'rinmasin
    const observer = new IntersectionObserver(([entry]) => setVisible(entry?.isIntersecting ?? false), {
      rootMargin: '800px 0px',
    })
    observer.observe(box)
    return () => observer.disconnect()
  }, [])

  useEffect(() => {
    const canvas = canvasRef.current
    if (canvas === null) return

    /*
      Ko'rinishdan chiqqan sahifa xotirani bo'shatadi: canvas o'lchami 0 — bitmap tashlanadi.
      Qaytib kelganda qayta chiziladi (joy egallovchi o'z o'lchamida qoladi, sakrash yo'q).
    */
    if (!visible) {
      canvas.width = 0
      canvas.height = 0
      setRendered(false)
      return
    }

    let task: RenderTask | null = null
    let cancelled = false

    void doc
      .getPage(number)
      .then((page) => {
      if (cancelled) return

      const base = page.getViewport({ scale: 1 })
      let ratio = Math.min(window.devicePixelRatio || 1, MAX_PIXEL_RATIO)
      // Juda kattalashganda canvas chegaralaridan oshmasin — tomoni ham, jami piksellari ham
      ratio = Math.min(
        ratio,
        MAX_CANVAS_SIDE / width,
        MAX_CANVAS_SIDE / height,
        Math.sqrt(MAX_CANVAS_PIXELS / (width * height)),
      )

      const viewport = page.getViewport({ scale: (width / base.width) * ratio })
      canvas.width = Math.floor(viewport.width)
      canvas.height = Math.floor(viewport.height)

      task = page.render({ canvas, viewport })
      task.promise.then(
        () => {
          if (!cancelled) setRendered(true)
        },
        () => {
          // Bekor qilingan chizish — keyingi kenglik bilan qayta chiziladi
        },
      )
      })
      .catch(() => {
        // Oyna yopildi yoki fayl almashdi — hujjat yopilgan, sahifa endi kerak emas
      })

    return () => {
      cancelled = true
      task?.cancel()
    }
  }, [doc, number, width, height, visible])

  // Sahifa olib tashlansa (oyna yopildi) — bitmap darhol bo'shaydi, GC kutilmaydi
  useEffect(
    () => () => {
      const canvas = canvasRef.current
      if (canvas !== null) {
        canvas.width = 0
        canvas.height = 0
      }
    },
    [],
  )

  return (
    <div
      ref={boxRef}
      data-pdf-page={number}
      className={cn('relative shrink-0 overflow-hidden rounded-sm bg-white shadow-floating')}
      style={{ width, height }}
    >
      <canvas ref={canvasRef} role="img" aria-label={label} className="block h-full w-full" />
      {!rendered && (
        <div className="absolute inset-0 grid place-items-center text-[var(--fg-tertiary)]">
          <LoaderCircle className="size-6 animate-spin text-black/30" />
        </div>
      )}
    </div>
  )
}
