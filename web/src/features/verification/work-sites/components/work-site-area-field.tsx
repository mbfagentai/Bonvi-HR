import {
  Check,
  Copy,
  Eraser,
  LoaderCircle,
  LocateFixed,
  MapPinOff,
  Search,
  Undo2,
} from 'lucide-react'
import {
  Component,
  Suspense,
  lazy,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ErrorInfo,
  type ReactNode,
} from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Button, Input, SegmentedControl, Skeleton, type SegmentOption } from '@/shared/ui'

import {
  SITE_BUFFER_MAX,
  SITE_BUFFER_MIN,
  SITE_RADIUS_MAX,
  SITE_RADIUS_MIN,
  SITE_VERTICES_MAX,
  SITE_VERTICES_MIN,
  WorkSiteShape,
} from '../api/work-sites-api'
import { geocode } from '../lib/nominatim'
import { formatPoint, roundPoint, samePoint, type LatLng } from '../lib/work-site-geometry'
import type { BaseLayer, MapFocus } from './work-site-map'

/** Leaflet lives only in this chunk (see `work-site-map.tsx`). */
const loadMap = () => import('./work-site-map')

const RADIUS_CHIPS = [100, 200, 300, 500] as const
const BUFFER_CHIPS = [0, 25, 50, 100] as const

/** Nominatim usage policy: at most one request per second. */
const SEARCH_INTERVAL_MS = 1000
const COPIED_MS = 2000

export interface AreaErrors {
  radius?: string
  buffer?: string
  /** Server rejected the geometry itself (self-intersection, too big, …). */
  area?: string
}

type SearchState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'found'; label: string }
  | { kind: 'notFound' }
  | { kind: 'failed' }

type LocateState = 'idle' | 'locating' | 'denied' | 'unavailable' | 'unsupported'

/**
 * «Hudud» section of the editor: shape switch, map, radius / buffer and the
 * read-only coordinates. The dialog owns the geometry (it builds the request
 * body); this component owns only the map UI state — search, base layer,
 * camera and the «you are here» dot.
 */
export function WorkSiteAreaField({
  shape,
  center,
  radiusText,
  vertices,
  bufferText,
  radius,
  selfIntersects,
  readOnly,
  disabled,
  errors,
  onShapeChange,
  onCenterChange,
  onRadiusTextChange,
  onVerticesChange,
  onBufferTextChange,
}: {
  shape: WorkSiteShape
  center: LatLng | null
  radiusText: string
  vertices: readonly LatLng[]
  bufferText: string
  /** Parsed radius for drawing; `null` while the input is not a valid number. */
  radius: number | null
  selfIntersects: boolean
  readOnly: boolean
  disabled: boolean
  errors: AreaErrors
  onShapeChange: (shape: WorkSiteShape) => void
  onCenterChange: (point: LatLng) => void
  onRadiusTextChange: (text: string) => void
  onVerticesChange: (vertices: LatLng[]) => void
  onBufferTextChange: (text: string) => void
}) {
  const { t, locale } = useLocale()

  const [baseLayer, setBaseLayer] = useState<BaseLayer>('map')
  const [focus, setFocus] = useState<MapFocus | null>(null)
  const [here, setHere] = useState<LatLng | null>(null)
  const [query, setQuery] = useState('')
  const [search, setSearch] = useState<SearchState>({ kind: 'idle' })
  const [locate, setLocate] = useState<LocateState>('idle')
  const [mapAttempt, setMapAttempt] = useState(0)

  const focusKey = useRef(0)
  const lastSearchAt = useRef(0)
  const searchAbort = useRef<AbortController | null>(null)

  // A failed chunk stays failed inside one `lazy()` — a retry needs a fresh one
  const WorkSiteMap = useMemo(() => lazy(loadMap), [mapAttempt])

  useEffect(() => () => searchAbort.current?.abort(), [])

  const flyTo = (point: LatLng, bounds: MapFocus['bounds'], frameRadius: number | null = null) => {
    focusKey.current += 1
    setFocus({ key: focusKey.current, point, bounds, radius: frameRadius })
  }

  const runSearch = async () => {
    const text = query.trim()
    if (text === '' || search.kind === 'loading') return
    if (Date.now() - lastSearchAt.current < SEARCH_INTERVAL_MS) return
    lastSearchAt.current = Date.now()

    searchAbort.current?.abort()
    const controller = new AbortController()
    searchAbort.current = controller
    setSearch({ kind: 'loading' })

    try {
      const hit = await geocode(text, locale, controller.signal)
      if (hit === null) {
        setSearch({ kind: 'notFound' })
        return
      }
      setSearch({ kind: 'found', label: hit.label })
      flyTo(hit.point, hit.bounds)
    } catch {
      if (!controller.signal.aborted) setSearch({ kind: 'failed' })
    }
  }

  /*
    HR usually sets a site up while standing in it — so for a circle the
    button also puts the centre there (it stays draggable). For a polygon it
    only moves the camera and shows the dot: a corner is never where HR stands.
  */
  const locateMe = () => {
    if (!window.isSecureContext || !('geolocation' in navigator)) {
      setLocate('unsupported')
      return
    }
    setLocate('locating')
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const point = roundPoint({ lat: position.coords.latitude, lng: position.coords.longitude })
        setHere(point)
        if (shape === WorkSiteShape.Circle) {
          onCenterChange(point)
          // Frame the whole circle, not a fixed zoom that may cut it off
          flyTo(point, null, radius)
        } else {
          flyTo(point, null)
        }
        setLocate('idle')
      },
      (error) => setLocate(error.code === error.PERMISSION_DENIED ? 'denied' : 'unavailable'),
      { enableHighAccuracy: true, timeout: 15_000, maximumAge: 30_000 },
    )
  }

  const changeVertices = (next: LatLng[]) => {
    if (disabled) return

    const added = next.length > vertices.length ? next.at(-1) : undefined
    if (added !== undefined) {
      if (vertices.length >= SITE_VERTICES_MAX) return
      // The ring is open and closes by itself: a click on the last or the
      // first vertex would be a zero-length edge (`obyekt.polygon_koordinata`)
      const last = vertices.at(-1)
      const first = vertices[0]
      if (last !== undefined && samePoint(last, added)) return
      if (first !== undefined && samePoint(first, added)) return
    }
    onVerticesChange(next)
  }

  const shapeOptions: Array<SegmentOption<WorkSiteShape>> = [
    { value: WorkSiteShape.Circle, label: t('attendance.workSites.shapeCircle') },
    { value: WorkSiteShape.Polygon, label: t('attendance.workSites.shapePolygon') },
  ]

  const layerOptions: Array<SegmentOption<BaseLayer>> = [
    { value: 'map', label: t('attendance.workSites.layerMap') },
    { value: 'satellite', label: t('attendance.workSites.layerSatellite') },
  ]

  const isCircle = shape === WorkSiteShape.Circle

  const hint = readOnly
    ? null
    : isCircle
      ? t('attendance.workSites.circleHint')
      : vertices.length >= SITE_VERTICES_MAX
        ? t('attendance.workSites.pointsMax', { n: SITE_VERTICES_MAX })
        : t('attendance.workSites.polygonHint')

  return (
    <div className="space-y-4">
      {!readOnly && (
        <div className="space-y-2">
          <p className="text-footnote font-medium text-[var(--fg-secondary)]">
            {t('attendance.workSites.shape')}
          </p>
          <SegmentedControl
            options={shapeOptions}
            value={shape}
            onChange={(next) => {
              if (!disabled) onShapeChange(next)
            }}
            className="w-full sm:w-auto"
            aria-label={t('attendance.workSites.shape')}
          />
        </div>
      )}

      {!readOnly && (
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <div className="min-w-[200px] flex-1">
              <Input
                icon={Search}
                type="search"
                value={query}
                disabled={disabled}
                placeholder={t('attendance.workSites.searchPlaceholder')}
                aria-label={t('attendance.workSites.searchPlaceholder')}
                onChange={(event) => setQuery(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key !== 'Enter') return
                  event.preventDefault()
                  void runSearch()
                }}
              />
            </div>
            <Button
              type="button"
              disabled={disabled || query.trim() === '' || search.kind === 'loading'}
              onClick={() => void runSearch()}
            >
              {search.kind === 'loading' && <LoaderCircle className="animate-spin" />}
              {t('attendance.workSites.searchAction')}
            </Button>
            <Button
              type="button"
              variant="soft"
              disabled={disabled || locate === 'locating'}
              onClick={locateMe}
            >
              {locate === 'locating' ? <LoaderCircle className="animate-spin" /> : <LocateFixed />}
              {t('attendance.workSites.myLocation')}
            </Button>
          </div>

          <SearchStatus state={search} />
          <LocateStatus state={locate} />
        </div>
      )}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <SegmentedControl
          options={layerOptions}
          value={baseLayer}
          onChange={setBaseLayer}
          size="sm"
          aria-label={t('attendance.workSites.layer')}
        />
        {hint !== null && <p className="text-footnote text-[var(--fg-tertiary)]">{hint}</p>}
      </div>

      <MapErrorBoundary key={mapAttempt} onRetry={() => setMapAttempt((value) => value + 1)}>
        <Suspense fallback={<MapPlaceholder />}>
          <WorkSiteMap
            shape={shape}
            center={center}
            radius={radius ?? 0}
            vertices={vertices}
            baseLayer={baseLayer}
            focus={focus}
            here={here}
            invalid={selfIntersects}
            readOnly={readOnly}
            label={t('attendance.workSites.mapLabel')}
            onCenterChange={(point) => {
              if (!disabled) onCenterChange(point)
            }}
            onVerticesChange={changeVertices}
          />
        </Suspense>
      </MapErrorBoundary>

      {isCircle ? (
        <MetersField
          label={t('attendance.workSites.radius')}
          hint={t('attendance.workSites.radiusHint', { min: SITE_RADIUS_MIN, max: SITE_RADIUS_MAX })}
          chips={RADIUS_CHIPS}
          text={radiusText}
          error={errors.radius}
          readOnly={readOnly}
          disabled={disabled}
          onChange={onRadiusTextChange}
        />
      ) : (
        <>
          {!readOnly && (
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                size="sm"
                disabled={disabled || vertices.length === 0}
                onClick={() => onVerticesChange(vertices.slice(0, -1))}
              >
                <Undo2 />
                {t('attendance.workSites.removeLastPoint')}
              </Button>
              <Button
                type="button"
                size="sm"
                variant="ghost"
                disabled={disabled || vertices.length === 0}
                onClick={() => onVerticesChange([])}
              >
                <Eraser />
                {t('attendance.workSites.clearPoints')}
              </Button>
              <span
                className={cn(
                  'ml-auto text-footnote tabular',
                  vertices.length < SITE_VERTICES_MIN
                    ? 'text-ios-orange'
                    : 'text-[var(--fg-secondary)]',
                )}
              >
                {vertices.length < SITE_VERTICES_MIN
                  ? t('attendance.workSites.pointsNeeded', {
                      n: vertices.length,
                      min: SITE_VERTICES_MIN,
                    })
                  : t('attendance.workSites.pointsCount', { n: vertices.length })}
              </span>
            </div>
          )}

          <MetersField
            label={t('attendance.workSites.edgeBuffer')}
            hint={t('attendance.workSites.edgeBufferHint', {
              min: SITE_BUFFER_MIN,
              max: SITE_BUFFER_MAX,
            })}
            chips={BUFFER_CHIPS}
            text={bufferText}
            error={errors.buffer}
            readOnly={readOnly}
            disabled={disabled}
            onChange={onBufferTextChange}
          />
        </>
      )}

      {errors.area !== undefined ? (
        <Alert tone="danger">{errors.area}</Alert>
      ) : (
        !isCircle &&
        selfIntersects && (
          <Alert tone="warning">{t('attendance.workSites.selfIntersectsHint')}</Alert>
        )
      )}

      <Coordinates shape={shape} center={center} vertices={vertices} />
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════════ parts */

function SearchStatus({ state }: { state: SearchState }) {
  const { t } = useLocale()

  switch (state.kind) {
    case 'idle':
    case 'loading':
      return null
    case 'found':
      return (
        <p className="truncate-1 text-footnote text-[var(--fg-secondary)]" title={state.label}>
          {t('attendance.workSites.searchFound', { name: state.label })}
        </p>
      )
    case 'notFound':
      return (
        <p role="alert" className="text-footnote text-ios-orange">
          {t('attendance.workSites.searchNotFound')}
        </p>
      )
    case 'failed':
      return (
        <p role="alert" className="text-footnote text-ios-red">
          {t('attendance.workSites.searchFailed')}
        </p>
      )
  }
}

function LocateStatus({ state }: { state: LocateState }) {
  const { t } = useLocale()

  if (state === 'idle' || state === 'locating') return null

  const message =
    state === 'denied'
      ? t('attendance.workSites.locateDenied')
      : state === 'unsupported'
        ? t('attendance.workSites.locateUnsupported')
        : t('attendance.workSites.locateUnavailable')

  return (
    <p role="alert" className="text-footnote text-ios-orange">
      {message}
    </p>
  )
}

function MapPlaceholder() {
  const { t } = useLocale()

  return (
    <div className="relative">
      <Skeleton className="h-[320px] w-full rounded-lg bg-[var(--bg-elevated)] sm:h-[420px]" />
      <p className="absolute inset-0 grid place-items-center text-footnote text-[var(--fg-tertiary)]">
        {t('attendance.workSites.mapLoading')}
      </p>
    </div>
  )
}

/**
 * The map chunk failed to load (offline, or a new deploy replaced the
 * hashed file). The rest of the form keeps working; «retry» re-imports.
 */
class MapErrorBoundary extends Component<
  { children: ReactNode; onRetry: () => void },
  { failed: boolean }
> {
  override state = { failed: false }

  static getDerivedStateFromError(): { failed: boolean } {
    return { failed: true }
  }

  override componentDidCatch(error: unknown, info: ErrorInfo): void {
    console.error('work-site map failed to load', error, info.componentStack)
  }

  override render(): ReactNode {
    if (!this.state.failed) return this.props.children
    return <MapLoadFailed onRetry={this.props.onRetry} />
  }
}

function MapLoadFailed({ onRetry }: { onRetry: () => void }) {
  const { t } = useLocale()

  return (
    <div className="flex h-[320px] flex-col items-center justify-center gap-3 rounded-lg border border-[var(--border)] bg-[var(--bg-elevated)] px-6 text-center sm:h-[420px]">
      <MapPinOff className="size-7 text-[var(--fg-tertiary)]" strokeWidth={1.6} />
      <p className="text-subhead text-[var(--fg-secondary)]">
        {t('attendance.workSites.mapLoadFailed')}
      </p>
      <Button type="button" size="sm" onClick={onRetry}>
        {t('common.retry')}
      </Button>
    </div>
  )
}

/**
 * Quick-pick chips plus a number input — one control for radius and buffer.
 * Chip look = the status chips of the attendance dashboard.
 */
function MetersField({
  label,
  hint,
  chips,
  text,
  error,
  readOnly,
  disabled,
  onChange,
}: {
  label: string
  hint: string
  chips: readonly number[]
  text: string
  error: string | undefined
  readOnly: boolean
  disabled: boolean
  onChange: (text: string) => void
}) {
  const { t } = useLocale()

  if (readOnly) {
    return (
      <p className="text-subhead">
        <span className="text-[var(--fg-secondary)]">{label}: </span>
        <span className="tabular font-medium">{t('attendance.workSites.meters', { n: text })}</span>
      </p>
    )
  }

  return (
    <div className="space-y-2">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">{label}</p>
      <div className="flex flex-wrap items-center gap-2">
        <div role="group" aria-label={label} className="flex flex-wrap gap-1">
          {chips.map((value) => {
            const selected = text.trim() === String(value)
            return (
              <button
                key={value}
                type="button"
                aria-pressed={selected}
                disabled={disabled}
                onClick={() => onChange(String(value))}
                className={cn(
                  'press flex h-7 items-center rounded-full px-2.5 text-caption1 font-medium whitespace-nowrap tabular',
                  'transition-colors duration-[120ms] ease-[var(--ease-ios)] disabled:opacity-40',
                  selected
                    ? 'bg-[var(--accent-soft)] text-[var(--accent-on-soft)]'
                    : 'bg-[var(--bg-elevated)] text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
                )}
              >
                {t('attendance.workSites.meters', { n: value })}
              </button>
            )
          })}
        </div>
        <div className="flex items-center gap-1.5">
          <div className="w-24">
            <Input
              inputMode="numeric"
              value={text}
              disabled={disabled}
              aria-label={label}
              aria-invalid={error === undefined ? undefined : true}
              className={cn('tabular', error !== undefined && 'border-ios-red')}
              onChange={(event) => onChange(event.target.value.replace(/\D+/gu, '').slice(0, 5))}
            />
          </div>
          <span className="text-subhead text-[var(--fg-secondary)]">
            {t('attendance.workSites.metersUnit')}
          </span>
        </div>
      </div>
      {error !== undefined ? (
        <p role="alert" className="text-footnote text-ios-red">
          {error}
        </p>
      ) : (
        <p className="text-footnote text-[var(--fg-tertiary)]">{hint}</p>
      )}
    </div>
  )
}

/**
 * Coordinates as text — HR cross-checks them in Google / Yandex maps.
 *
 * A read-only field, not plain text: `navigator.clipboard` exists only in a
 * secure context, so on plain HTTP the copy button selects the text instead
 * (same fallback as the device token dialog).
 */
function Coordinates({
  shape,
  center,
  vertices,
}: {
  shape: WorkSiteShape
  center: LatLng | null
  vertices: readonly LatLng[]
}) {
  const { t } = useLocale()

  const text =
    shape === WorkSiteShape.Circle
      ? center === null
        ? ''
        : formatPoint(center)
      : vertices.map((vertex) => formatPoint(vertex)).join('\n')

  const empty = text === ''
  const rows = shape === WorkSiteShape.Circle ? 1 : Math.min(Math.max(vertices.length, 1), 6)

  return (
    <div className="space-y-2">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">
        {shape === WorkSiteShape.Circle
          ? t('attendance.workSites.coordinatesCenter')
          : t('attendance.workSites.coordinatesPoints')}
      </p>
      {empty ? (
        <p className="text-footnote text-[var(--fg-tertiary)]">
          {shape === WorkSiteShape.Circle
            ? t('attendance.workSites.centerMissing')
            : t('attendance.workSites.pointsMissing')}
        </p>
      ) : (
        <CopyText text={text} rows={rows} label={t('attendance.workSites.coordinates')} />
      )}
    </div>
  )
}

function CopyText({ text, rows, label }: { text: string; rows: number; label: string }) {
  const { t } = useLocale()
  const fieldRef = useRef<HTMLTextAreaElement>(null)
  const [state, setState] = useState<'idle' | 'copied' | 'failed'>('idle')

  useEffect(() => {
    if (state !== 'copied') return
    const timer = window.setTimeout(() => setState('idle'), COPIED_MS)
    return () => window.clearTimeout(timer)
  }, [state])

  const copy = async () => {
    try {
      // Undefined outside a secure context
      if (navigator.clipboard === undefined) throw new Error('clipboard')
      await navigator.clipboard.writeText(text)
      setState('copied')
    } catch {
      fieldRef.current?.focus()
      fieldRef.current?.select()
      setState('failed')
    }
  }

  return (
    <div className="space-y-2">
      <div className="flex items-start gap-2">
        <textarea
          ref={fieldRef}
          readOnly
          rows={rows}
          value={text}
          aria-label={label}
          className={cn(
            'min-w-0 flex-1 resize-none rounded-md border border-[var(--border-strong)] px-3.5 py-2.5',
            'bg-[var(--bg-elevated)] font-mono text-footnote leading-relaxed text-[var(--fg)] outline-none',
            'focus:border-[var(--accent)] focus:ring-4 focus:ring-[var(--accent)]/10',
          )}
        />
        <Button
          type="button"
          size="sm"
          variant={state === 'copied' ? 'soft' : 'secondary'}
          className="mt-1 shrink-0"
          onClick={() => void copy()}
        >
          {state === 'copied' ? <Check /> : <Copy />}
          {state === 'copied' ? t('attendance.copied') : t('attendance.tokenCopy')}
        </Button>
      </div>
      {state === 'failed' && (
        <p role="alert" className="text-footnote text-ios-orange">
          {t('attendance.copyFailed')}
        </p>
      )}
    </div>
  )
}
