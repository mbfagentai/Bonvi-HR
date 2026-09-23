import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Badge, Button, Dialog, TextArea, TextField } from '@/shared/ui'

import {
  SITE_ADDRESS_MAX,
  SITE_BUFFER_DEFAULT,
  SITE_BUFFER_MAX,
  SITE_BUFFER_MIN,
  SITE_NAME_MAX,
  SITE_RADIUS_DEFAULT,
  SITE_RADIUS_MAX,
  SITE_RADIUS_MIN,
  SITE_VERTICES_MAX,
  SITE_VERTICES_MIN,
  WorkSiteShape,
  type WorkSite,
  type WorkSiteBody,
} from '../api/work-sites-api'
import { useSaveWorkSite } from '../api/work-sites-queries'
import { parseMeters, polygonSelfIntersects, type LatLng } from '../lib/work-site-geometry'
import { WorkSiteActiveField } from './work-site-active-field'
import { WorkSiteAreaField } from './work-site-area-field'

type Field = 'name' | 'address' | 'radius' | 'buffer' | 'area'

/** Server error code → the field it is shown under. Unknown codes go to the top alert. */
const FIELD_OF_CODE: Readonly<Record<string, Field>> = {
  'malumotnoma.nom_bosh': 'name',
  'malumotnoma.nom_uzun': 'name',
  'malumotnoma.nom_band': 'name',
  'obyekt.manzil_uzun': 'address',
  'davomat.obyekt_radiusi': 'radius',
  'obyekt.polygon_zaxira': 'buffer',
  'obyekt.shakl_notogri': 'area',
  'obyekt.polygon_nuqtalar_soni': 'area',
  'obyekt.polygon_koordinata': 'area',
  'obyekt.polygon_kesishadi': 'area',
  'obyekt.polygon_katta': 'area',
  'davomat.obyekt_kenglik': 'area',
  'davomat.obyekt_uzunlik': 'area',
}

/** Geometry of the editing session — both shapes are kept, switching loses nothing. */
interface Geometry {
  shape: WorkSiteShape
  center: LatLng | null
  radiusText: string
  vertices: LatLng[]
  bufferText: string
}

function initialGeometry(site: WorkSite | null): Geometry {
  if (site === null) {
    return {
      shape: WorkSiteShape.Circle,
      center: null,
      radiusText: String(SITE_RADIUS_DEFAULT),
      vertices: [],
      bufferText: String(SITE_BUFFER_DEFAULT),
    }
  }

  return {
    shape: site.shape,
    // For a polygon this is the server-computed centroid: switching such a
    // site to a circle starts from its middle instead of an empty map
    center: { lat: site.latitude, lng: site.longitude },
    // A polygon's stored radius is a filler value nobody chose: switching
    // it to a circle must make HR pick a radius, not inherit 200 m silently
    radiusText: site.shape === WorkSiteShape.Polygon ? '' : String(site.radiusMeters),
    vertices: site.polygon === null ? [] : site.polygon.map(({ lat, lng }) => ({ lat, lng })),
    bufferText: String(site.edgeBufferMeters),
  }
}

function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * Create / edit / view one work site (SPEC-joylashuvlar §D).
 *
 * A dialog, like every other verification editor (devices, schedules). The
 * map inside is lazy — see `work-site-area-field.tsx`.
 *
 * `readOnly` — the user has `attendance.view` but not `attendance.manage`:
 * the same dialog shows where the site is, with nothing editable.
 */
export function WorkSiteFormDialog({
  open,
  onClose,
  site,
  readOnly,
  activeCount,
}: {
  open: boolean
  onClose: () => void
  /** `null` — new site. */
  site: WorkSite | null
  readOnly: boolean
  /** Active sites in the list — to warn before turning off the last one. */
  activeCount: number
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveWorkSite()

  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [note, setNote] = useState('')
  const [geometry, setGeometry] = useState<Geometry>(() => initialGeometry(null))
  /** Remounts the map on every opening, so it starts at the opened site. */
  const [session, setSession] = useState(0)

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: Field; code: string } | null>(null)

  useEffect(() => {
    if (!open) return
    setName(site?.name ?? '')
    setAddress(site?.address ?? '')
    setNote(site?.note ?? '')
    setGeometry(initialGeometry(site))
    setSession((value) => value + 1)
    setFailure(null)
    setFieldError(null)
    save.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, site])

  const pending = save.isPending
  const isEdit = site !== null
  const isCircle = geometry.shape === WorkSiteShape.Circle

  const radius = parseMeters(geometry.radiusText)
  const buffer = parseMeters(geometry.bufferText)
  const radiusValid = radius !== null && radius >= SITE_RADIUS_MIN && radius <= SITE_RADIUS_MAX
  const bufferValid = buffer !== null && buffer >= SITE_BUFFER_MIN && buffer <= SITE_BUFFER_MAX

  const selfIntersects = useMemo(
    () => !isCircle && polygonSelfIntersects(geometry.vertices),
    [isCircle, geometry.vertices],
  )

  const geometryReady = isCircle
    ? geometry.center !== null && radiusValid
    : geometry.vertices.length >= SITE_VERTICES_MIN &&
      geometry.vertices.length <= SITE_VERTICES_MAX &&
      bufferValid

  const incomplete = name.trim() === '' || !geometryReady

  /** Any geometry edit drops the stale server verdict about the old shape. */
  const patchGeometry = (patch: Partial<Geometry>, clears: readonly Field[]) => {
    setGeometry((previous) => ({ ...previous, ...patch }))
    if (fieldError !== null && clears.includes(fieldError.field)) setFieldError(null)
  }

  const submit = () => {
    if (incomplete || readOnly) return
    setFailure(null)
    setFieldError(null)

    const common = {
      name: name.trim(),
      address: orNull(address),
      note: orNull(note),
    }

    let body: WorkSiteBody
    if (isCircle) {
      if (geometry.center === null || radius === null) return
      body = {
        ...common,
        shape: WorkSiteShape.Circle,
        latitude: geometry.center.lat,
        longitude: geometry.center.lng,
        radiusMeters: radius,
      }
    } else {
      if (buffer === null) return
      body = {
        ...common,
        shape: WorkSiteShape.Polygon,
        polygon: geometry.vertices,
        edgeBufferMeters: buffer,
      }
    }

    save.mutate(
      { id: site?.id ?? null, body },
      {
        onSuccess: onClose,
        onError: (error) => {
          const code = problemCode(error)
          const field = FIELD_OF_CODE[code]
          if (field === undefined) setFailure(code)
          else setFieldError({ field, code })
        },
      },
    )
  }

  const fieldMessage = (field: Field): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  /* Client-side range messages reuse the server's own texts */
  const radiusError =
    fieldMessage('radius') ??
    (radiusValid || geometry.radiusText === '' ? undefined : errorMessage('davomat.obyekt_radiusi'))
  const bufferError =
    fieldMessage('buffer') ??
    (bufferValid || geometry.bufferText === '' ? undefined : errorMessage('obyekt.polygon_zaxira'))

  const title = readOnly
    ? t('attendance.workSites.viewTitle')
    : isEdit
      ? t('attendance.workSites.editTitle')
      : t('attendance.workSites.createTitle')

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!pending}
      title={title}
      description={isEdit ? site.name : t('attendance.workSites.createSubtitle')}
      footer={
        readOnly ? (
          <Button type="button" onClick={onClose}>
            {t('common.close')}
          </Button>
        ) : (
          <>
            <Button type="button" onClick={onClose} disabled={pending}>
              {t('common.cancel')}
            </Button>
            <Button
              type="button"
              variant="primary"
              onClick={submit}
              disabled={pending || incomplete}
            >
              {pending && <LoaderCircle className="animate-spin" />}
              {pending
                ? t('catalog.saving')
                : isEdit
                  ? t('common.save')
                  : t('attendance.workSites.create')}
            </Button>
          </>
        )
      }
    >
      <div className="space-y-6">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        <Section title={t('attendance.workSites.sectionBasics')}>
          {readOnly ? (
            <ReadOnlyBasics site={site} />
          ) : (
            <>
              <TextField
                label={t('attendance.workSites.name')}
                hint={t('attendance.workSites.nameHint')}
                value={name}
                required
                maxLength={SITE_NAME_MAX}
                disabled={pending}
                error={fieldMessage('name')}
                onChange={(event) => {
                  setName(event.target.value)
                  if (fieldError?.field === 'name') setFieldError(null)
                }}
              />
              <TextField
                label={t('attendance.workSites.address')}
                hint={t('attendance.workSites.addressHint')}
                value={address}
                maxLength={SITE_ADDRESS_MAX}
                disabled={pending}
                error={fieldMessage('address')}
                onChange={(event) => {
                  setAddress(event.target.value)
                  if (fieldError?.field === 'address') setFieldError(null)
                }}
              />
              <TextArea
                label={t('attendance.workSites.note')}
                value={note}
                rows={2}
                disabled={pending}
                onChange={(event) => setNote(event.target.value)}
              />
            </>
          )}
        </Section>

        <Section title={t('attendance.workSites.sectionArea')}>
          <WorkSiteAreaField
              key={session}
              shape={geometry.shape}
              center={geometry.center}
              radiusText={geometry.radiusText}
              vertices={geometry.vertices}
              bufferText={geometry.bufferText}
              radius={radius}
              selfIntersects={selfIntersects}
              readOnly={readOnly}
              disabled={pending}
              errors={{
                radius: radiusError,
                buffer: bufferError,
                area: fieldMessage('area'),
              }}
              onShapeChange={(shape) => patchGeometry({ shape }, ['area', 'radius', 'buffer'])}
              onCenterChange={(center) => patchGeometry({ center }, ['area'])}
              onRadiusTextChange={(radiusText) => patchGeometry({ radiusText }, ['radius'])}
              onVerticesChange={(vertices) => patchGeometry({ vertices }, ['area'])}
              onBufferTextChange={(bufferText) => patchGeometry({ bufferText }, ['buffer'])}
            />
        </Section>

        {isEdit && !readOnly && (
          <WorkSiteActiveField
            id={site.id}
            name={site.name}
            active={site.isActive}
            isLastActive={site.isActive && activeCount <= 1}
            disabled={pending}
            // The server writes the deactivation reason into the note; keep
            // the open form in step so a later Save doesn't overwrite it
            onReason={setNote}
          />
        )}
      </div>
    </Dialog>
  )
}

/** View mode: name, address, note and status as text. */
function ReadOnlyBasics({ site }: { site: WorkSite | null }) {
  const { t } = useLocale()
  if (site === null) return null

  return (
    <dl className="grid gap-3 text-subhead sm:grid-cols-[auto_1fr] sm:gap-x-6">
      <dt className="text-[var(--fg-secondary)]">{t('attendance.workSites.name')}</dt>
      <dd className="font-medium">{site.name}</dd>
      <dt className="text-[var(--fg-secondary)]">{t('attendance.workSites.address')}</dt>
      <dd>{site.address ?? '—'}</dd>
      <dt className="text-[var(--fg-secondary)]">{t('attendance.workSites.note')}</dt>
      <dd className="whitespace-pre-line">{site.note ?? '—'}</dd>
      <dt className="text-[var(--fg-secondary)]">{t('attendance.workSites.columnStatus')}</dt>
      <dd>
        <Badge tone={site.isActive ? 'success' : 'neutral'} size="sm">
          {site.isActive
            ? t('attendance.workSites.statusActive')
            : t('attendance.workSites.statusInactive')}
        </Badge>
      </dd>
    </dl>
  )
}

/** Same look as the device dialog's `Section`. */
function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-4 rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
      <h3 className="text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
        {title}
      </h3>
      {children}
    </section>
  )
}
