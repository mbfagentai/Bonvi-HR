import { Link } from '@tanstack/react-router'
import {
  AlarmClock,
  ChevronRight,
  Cpu,
  KeyRound,
  LoaderCircle,
  Pencil,
  Plus,
  ShieldAlert,
  Trash2,
} from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { SETTINGS_MANAGE } from '@/features/settings/api/settings-api'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  Dialog,
  EmptyState,
  PageHeader,
  SegmentedControl,
  Switch,
  dataColumnHelper,
  type DataColumn,
  type SegmentOption,
} from '@/shared/ui'

import type { AttendanceDevice, AttendanceDeviceCreated } from '../api/attendance-api'
import {
  useAttendanceDevices,
  useDeleteDevice,
  useFormerEmployeeAlerts,
  useRotateDeviceToken,
  useSetDeviceActive,
} from '../api/attendance-queries'
import {
  DIRECTION_ROLE_LOOK,
  agoParts,
  looksRejected,
} from '../lib/attendance-manage-presentation'
import { ATTENDANCE_MANAGE, ATTENDANCE_VIEW } from '../lib/attendance-presentation'
import { AttendanceDeviceFormDialog } from './attendance-device-form-dialog'
import { AttendanceDeviceTokenDialog, type DeviceSecret } from './attendance-device-token-dialog'
import {
  AttendanceLoadError,
  AttendanceNoAccess,
  AttendancePageTitle,
} from './attendance-manage-states'
import { AttendanceRetentionCard } from './attendance-retention-card'

const helper = dataColumnHelper<AttendanceDevice>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: AttendanceDevice[] = []

type DeviceView = 'active' | 'all'

/**
 * Qurilmalar — turniket terminallari reyestri va «qabul tirikmi?» paneli.
 *
 * ⭐ Hangfire paneli yo'q (SPEC §1.2 #12), shu sababli qabul yo'lining
 * sog'ligi SHU sahifada ko'rinadi (§8.7): oxirgi ko'rinish, 24 soatdagi
 * o'tishlar va soat belgisi. Uchalasi birga «terminal ulangan va
 * hodisalari tabelga tushyapti» degan savolga javob beradi; bittasi
 * yetishmasa, javob yarim bo'ladi.
 *
 * ⭐ Ishdan ketgan xodimning o'tishi shu yerda ham QIZIL qator bo'lib
 * turadi (§8.7): tuzatish aynan shu yerda — operator allaqachon o'sha
 * odamni ichkariga kiritgan terminalga qarab turibdi. Qator yopilmaydi
 * va yashirilmaydi; u 24 soat ichida yangi hodisa bo'lmasa o'zi ketadi.
 *
 * Ruxsat: sahifa to'liq `attendance.manage` (§7.3) — token prefikslari,
 * IP ro'yxati va binoning kirish joylari oddiy ko'ruvchiga kerak emas.
 */
export function AttendanceDevicesPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canManage = hasPermission(ATTENDANCE_MANAGE)
  // Ogohlantirish endpoint'i `attendance.view` ostida — busiz so'rov 403 bo'lardi
  const canView = hasPermission(ATTENDANCE_VIEW)
  /*
    Saqlash muddatlari tizim sozlamasi — `GET/PUT /api/settings` faqat shu
    ruxsat bilan ochiq. Ruxsat bo'lmasa karta UMUMAN chizilmaydi (so'rov ham
    yuborilmaydi): ko'rinadigan-u saqlab bo'lmaydigan forma yolg'on va'da.
  */
  const canManageSettings = hasPermission(SETTINGS_MANAGE)

  const [view, setView] = useState<DeviceView>('active')
  const devices = useAttendanceDevices(view === 'all', canManage)
  const alerts = useFormerEmployeeAlerts(canManage && canView)

  const setActive = useSetDeviceActive()

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<AttendanceDevice | null>(null)
  const [secret, setSecret] = useState<DeviceSecret | null>(null)
  const [rotating, setRotating] = useState<AttendanceDevice | null>(null)
  const [deleting, setDeleting] = useState<AttendanceDevice | null>(null)
  const [deactivating, setDeactivating] = useState<AttendanceDevice | null>(null)

  const rows = devices.data ?? EMPTY
  const skewCount = rows.filter((device) => device.isActive && device.skewWarning).length

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (device: AttendanceDevice) => {
    setEditing(device)
    setFormOpen(true)
  }

  /*
    Yoqish — darhol (xavfsiz amal), o'chirish — tasdiq bilan: o'chirilgan
    terminalning o'tishlari 403 bilan rad etiladi va tabelga tushmaydi.
    Tasodifiy tegish butun kirish joyini «ko'r» qilib qo'yardi.
  */
  const toggleActive = (device: AttendanceDevice, next: boolean) => {
    if (next) {
      setActive.mutate({ id: device.id, active: true })
    } else {
      setDeactivating(device)
    }
  }

  const columns = useMemo<Array<DataColumn<AttendanceDevice>>>(() => {
    const definitions: Array<DataColumn<AttendanceDevice>> = [
      helper.accessor('name', {
        header: t('attendance.deviceName'),
        meta: { width: '260px' },
        cell: ({ row }) => {
          const device = row.original
          return (
            <div className="min-w-0">
              <p className={cn('truncate-1 font-medium', !device.isActive && 'text-[var(--fg-tertiary)]')}>
                {device.name}
              </p>
              <p className="truncate-1 font-mono text-caption2 text-[var(--fg-tertiary)]">
                {[device.model, device.serialNumber].filter(Boolean).join(' · ') || '—'}
              </p>
            </div>
          )
        },
      }),

      helper.display({
        id: 'location',
        header: t('attendance.location'),
        // Ortgan joyni joylashuv oladi — erkin matn va ma'lumotnoma nomlari
        meta: { minWidth: '200px', hideOnMobile: true },
        cell: ({ row }) => {
          const device = row.original
          const catalog = [device.departmentName, device.legalEntityName].filter(Boolean).join(' · ')
          return (
            <div className="min-w-0">
              <p className="truncate-1">{device.locationNote ?? '—'}</p>
              {catalog !== '' && (
                <p className="truncate-1 text-caption1 text-[var(--fg-secondary)]">{catalog}</p>
              )}
            </div>
          )
        },
      }),

      helper.accessor('directionRole', {
        header: t('attendance.directionRole'),
        meta: { width: '160px', hideOnMobile: true },
        cell: ({ row }) => {
          const look = DIRECTION_ROLE_LOOK[row.original.directionRole]
          const Icon = look.icon
          return (
            <span className="flex items-center gap-1.5 text-[var(--fg-secondary)]">
              <Icon className="size-4 shrink-0" />
              <span className="truncate-1">{t(look.labelKey)}</span>
            </span>
          )
        },
      }),

      helper.accessor('isActive', {
        header: t('attendance.active'),
        // iOS almashtirgichi 51px + katak to'ldirishi (32px) ≈ 83px, zaxira bilan 96px
        meta: { width: '96px' },
        cell: ({ row }) => (
          // Qator bosilsa tahrirlash ochiladi — almashtirgich bosilishi unga «oqib» o'tmasin
          <span onClick={(event) => event.stopPropagation()} onKeyDown={(event) => event.stopPropagation()}>
            <Switch
              checked={row.original.isActive}
              disabled={setActive.isPending && setActive.variables?.id === row.original.id}
              onChange={(next) => toggleActive(row.original, next)}
              aria-label={`${row.original.name} — ${t('attendance.active')}`}
            />
          </span>
        ),
      }),

      helper.accessor('lastSeenAt', {
        header: t('attendance.lastSeen'),
        meta: { width: '180px' },
        cell: ({ row }) => <LastSeen device={row.original} />,
      }),

      helper.accessor('passCount24h', {
        header: t('attendance.passes24h'),
        meta: { width: '120px', align: 'end', hideOnMobile: true },
        cell: ({ row }) => (
          <span
            className={cn(
              'tabular font-medium',
              row.original.passCount24h === 0 && 'text-[var(--fg-tertiary)]',
            )}
          >
            {num(row.original.passCount24h)}
          </span>
        ),
      }),

      helper.accessor('lastSkewSeconds', {
        header: t('attendance.clock'),
        meta: { width: '140px', hideOnMobile: true },
        cell: ({ row }) => <ClockBadge device={row.original} />,
      }),

      helper.accessor('tokenPrefix', {
        header: t('attendance.tokenPrefix'),
        enableSorting: false,
        meta: { width: '130px', hideOnMobile: true },
        cell: ({ row }) => (
          <span className="font-mono text-footnote text-[var(--fg-secondary)]">
            {row.original.tokenPrefix === '' ? '—' : `${row.original.tokenPrefix}…`}
          </span>
        ),
      }),

      helper.display({
        id: 'actions',
        header: '',
        // Uchta ikonkali tugma (32px) + ikki oraliq (6px) = 108px + katak (32px) → 150px
        meta: { align: 'end', width: '150px' },
        cell: ({ row }) => {
          const device = row.original
          return (
            <span className="flex justify-end gap-1.5">
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                title={t('common.edit')}
                aria-label={t('common.edit')}
                onClick={(event) => {
                  event.stopPropagation()
                  openEdit(device)
                }}
              >
                <Pencil />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                title={t('attendance.rotateToken')}
                aria-label={t('attendance.rotateToken')}
                onClick={(event) => {
                  event.stopPropagation()
                  setRotating(device)
                }}
              >
                <KeyRound />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                title={t('common.delete')}
                aria-label={t('common.delete')}
                onClick={(event) => {
                  event.stopPropagation()
                  setDeleting(device)
                }}
              >
                <Trash2 className="text-[var(--danger-text)]" />
              </Button>
            </span>
          )
        },
      }),
    ]

    return definitions
    // `toggleActive` va `openEdit` faqat holat o'rnatuvchilarni chaqiradi — barqaror
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [t, num, setActive.isPending, setActive.variables])

  if (!canManage) {
    return <AttendanceNoAccess icon={Cpu} title={t('attendance.devicesTitle')} />
  }

  const viewOptions: Array<SegmentOption<DeviceView>> = [
    { value: 'active', label: t('attendance.devicesActiveOnly') },
    { value: 'all', label: t('attendance.devicesAll') },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title={<AttendancePageTitle icon={Cpu} title={t('attendance.devicesTitle')} />}
        subtitle={
          devices.isPending
            ? t('common.loading')
            : t('attendance.devicesSubtitle', { n: num(rows.length) })
        }
        actions={
          <Button variant="create" onClick={openCreate}>
            <Plus />
            {t('attendance.deviceCreate')}
          </Button>
        }
      />

      {/*
        Ishdan ketgan odam binoga kirdi — qizil, yopilmaydigan qator, JADVAL
        USTIDA (§8.7). Bu modulning yagona qizil yuzasi taxtadan tashqarida:
        soat va moslanmagan o'tishlar sariq, bu esa xavfsizlik hodisasi.
      */}
      {alerts.length > 0 && (
        <Link
          to="/verifikatsiya/jonli"
          className="press flex items-center gap-3 rounded-lg bg-ios-red/10 px-4 py-3 text-ios-red"
        >
          <ShieldAlert className="size-5 shrink-0" strokeWidth={2.25} />
          <span className="min-w-0 flex-1 text-subhead font-semibold">
            {t('attendance.formerEmployeeShort', { n: num(alerts.length) })}
          </span>
          <ChevronRight className="size-5 shrink-0" />
        </Link>
      )}

      {skewCount > 0 && (
        <Alert tone="warning" title={t('attendance.devicesClockWarningTitle', { n: num(skewCount) })}>
          {t('attendance.devicesClockWarningText')}
        </Alert>
      )}

      {setActive.isError && (
        <Alert tone="danger">{errorMessage(problemCode(setActive.error))}</Alert>
      )}

      <Card className="overflow-hidden">
        <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
          <SegmentedControl
            options={viewOptions}
            value={view}
            onChange={setView}
            size="sm"
            aria-label={t('common.filters')}
          />
        </div>

        {devices.isError ? (
          <AttendanceLoadError
            error={devices.error}
            onRetry={() => void devices.refetch()}
            retrying={devices.isFetching}
          />
        ) : (
          <DataTable
            columns={columns}
            data={rows}
            getRowId={(device) => device.id}
            loading={devices.isPending}
            onRowClick={openEdit}
            rowLabel={(device) => device.name}
            empty={
              <EmptyState
                icon={Cpu}
                title={
                  view === 'active'
                    ? t('attendance.devicesEmptyTitle')
                    : t('attendance.devicesEmptyAllTitle')
                }
                description={t('attendance.devicesEmptyText')}
                action={
                  <Button variant="create" onClick={openCreate}>
                    <Plus />
                    {t('attendance.deviceCreate')}
                  </Button>
                }
              />
            }
          />
        )}
      </Card>

      {/*
        Saqlash muddatlari — ro'yxatdan KEYIN: qurilmalar kundalik ish, muddat
        esa bir marta sozlanadi. Tizim sozlamalari sahifasidan shu yerga
        ko'chgan (sabab `AttendanceRetentionCard` izohida).
      */}
      {canManageSettings && <AttendanceRetentionCard />}

      <AttendanceDeviceFormDialog
        open={formOpen}
        onClose={() => setFormOpen(false)}
        device={editing}
        onCreated={(created: AttendanceDeviceCreated) => {
          setFormOpen(false)
          setSecret({ ...created, reason: 'created' })
        }}
      />
      <AttendanceDeviceTokenDialog secret={secret} onClose={() => setSecret(null)} />
      <RotateTokenDialog
        device={rotating}
        onClose={() => setRotating(null)}
        onRotated={(created) => {
          setRotating(null)
          setSecret({ ...created, reason: 'rotated' })
        }}
      />
      <DeactivateDialog device={deactivating} onClose={() => setDeactivating(null)} />
      <DeleteDeviceDialog
        device={deleting}
        onClose={() => setDeleting(null)}
        onDeactivate={(device) => {
          setDeleting(null)
          setDeactivating(device)
        }}
      />
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════ jadval kataklari */

/**
 * Oxirgi ko'rinish — nisbiy vaqt, aniq vaqt esa `title` da.
 *
 * Terminal gaplashyapti-yu, hodisalari rad etilyapti (`looksRejected`) —
 * sariq izoh: bu holatda qolgan hamma raqam «joyida» ko'rinadi va tabel
 * jimgina bo'shab boradi.
 */
function LastSeen({ device }: { device: AttendanceDevice }) {
  const { t, num, dateTime } = useLocale()

  if (device.lastSeenAt === null) {
    return <span className="text-footnote text-[var(--fg-tertiary)]">{t('attendance.neverSeen')}</span>
  }

  const parts = agoParts(device.lastSeenAt)
  const label =
    parts === null
      ? dateTime(device.lastSeenAt)
      : parts.unit === 'now'
        ? t('attendance.agoNow')
        : parts.unit === 'minutes'
          ? t('attendance.agoMinutes', { n: num(parts.value) })
          : t('attendance.agoHours', { n: num(parts.value) })

  return (
    <div className="min-w-0">
      <p
        className={cn('tabular truncate-1', parts === null && 'text-ios-orange')}
        title={dateTime(device.lastSeenAt)}
      >
        {label}
      </p>
      {looksRejected(device.lastSeenAt, device.lastEventAt) && (
        <p className="truncate-1 text-caption1 text-ios-orange">{t('attendance.eventsRejected')}</p>
      )}
    </div>
  )
}

/**
 * Soat belgisi — server hisoblagan `skewWarning` dan (klient 300 soniya
 * chegarasini TAKRORLAMAYDI, §6.2). Siljish soni izoh uchun.
 */
function ClockBadge({ device }: { device: AttendanceDevice }) {
  const { t, num } = useLocale()

  if (device.lastSkewSeconds === null) {
    return <span className="text-[var(--fg-tertiary)]">—</span>
  }

  const seconds = Math.abs(device.lastSkewSeconds)
  const amount =
    seconds >= 60
      ? t('attendance.skewMinutes', { n: num(Math.round(seconds / 60)) })
      : t('attendance.skewSeconds', { n: num(seconds) })

  return device.skewWarning ? (
    <Badge tone="warning" size="sm" title={amount}>
      <AlarmClock className="size-3" strokeWidth={2.5} />
      {amount}
    </Badge>
  ) : (
    <Badge tone="success" size="sm" title={amount}>
      {t('attendance.clockOk')}
    </Badge>
  )
}

/* ═══════════════════════════════════════════════════════════ tasdiqlash oynalari */

/** Token almashtirish — eski token SHU ZAHOTI o'ladi, shuning uchun tasdiq bilan. */
function RotateTokenDialog({
  device,
  onClose,
  onRotated,
}: {
  device: AttendanceDevice | null
  onClose: () => void
  onRotated: (created: AttendanceDeviceCreated) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const rotate = useRotateDeviceToken()

  useEffect(() => {
    if (device !== null) rotate.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [device])

  if (device === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!rotate.isPending}
      title={t('attendance.rotateToken')}
      description={device.name}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={rotate.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            disabled={rotate.isPending}
            onClick={() => rotate.mutate(device.id, { onSuccess: onRotated })}
          >
            {rotate.isPending && <LoaderCircle className="animate-spin" />}
            {t('attendance.rotateToken')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">{t('attendance.rotateConfirm')}</p>
        {rotate.isError && <Alert tone="danger">{errorMessage(problemCode(rotate.error))}</Alert>}
      </div>
    </Dialog>
  )
}

function DeactivateDialog({
  device,
  onClose,
}: {
  device: AttendanceDevice | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const setActive = useSetDeviceActive()

  useEffect(() => {
    if (device !== null) setActive.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [device])

  if (device === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!setActive.isPending}
      title={t('attendance.deactivateTitle')}
      description={device.name}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={setActive.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            disabled={setActive.isPending}
            onClick={() =>
              setActive.mutate({ id: device.id, active: false }, { onSuccess: onClose })
            }
          >
            {setActive.isPending && <LoaderCircle className="animate-spin" />}
            {t('catalog.deactivate')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">{t('attendance.deactivateConfirm')}</p>
        {setActive.isError && (
          <Alert tone="danger">{errorMessage(problemCode(setActive.error))}</Alert>
        )}
      </div>
    </Dialog>
  )
}

/**
 * Qurilmani o'chirish.
 *
 * ⚠️ O'tishi BOR qurilma o'chirilmaydi (`davomat.qurilma_otishlari_bor`,
 * 409): jurnal o'z manbasini yo'qotmasligi kerak. Server shunday javob
 * bersa, oyna sababni aytadi va shu yerning o'zida FAOLSIZLANTIRISHNI
 * taklif qiladi — HR ga yo'l ko'rsatilmasa, u qurilmani jadvaldan yo'q
 * qilishning boshqa usulini izlab yurardi.
 */
function DeleteDeviceDialog({
  device,
  onClose,
  onDeactivate,
}: {
  device: AttendanceDevice | null
  onClose: () => void
  onDeactivate: (device: AttendanceDevice) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteDevice()

  useEffect(() => {
    if (device !== null) remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [device])

  if (device === null) return null

  const code = remove.isError ? problemCode(remove.error) : null
  const hasPasses = code === 'davomat.qurilma_otishlari_bor'

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('attendance.deviceDeleteTitle')}
      description={device.name}
      footer={
        hasPasses ? (
          <>
            <Button type="button" onClick={onClose}>
              {t('common.close')}
            </Button>
            {device.isActive && (
              <Button type="button" variant="primary" onClick={() => onDeactivate(device)}>
                {t('catalog.deactivate')}
              </Button>
            )}
          </>
        ) : (
          <>
            <Button type="button" onClick={onClose} disabled={remove.isPending}>
              {t('common.cancel')}
            </Button>
            <Button
              type="button"
              variant="danger"
              disabled={remove.isPending}
              onClick={() => remove.mutate(device.id, { onSuccess: onClose })}
            >
              {remove.isPending && <LoaderCircle className="animate-spin" />}
              {t('common.delete')}
            </Button>
          </>
        )
      }
    >
      <div className="space-y-4">
        {hasPasses ? (
          <Alert tone="warning" title={errorMessage(code)}>
            {t('attendance.deviceHasPassesHint')}
          </Alert>
        ) : (
          <p className="text-subhead text-[var(--fg-secondary)]">{t('attendance.deleteConfirm')}</p>
        )}
        {code !== null && !hasPasses && <Alert tone="danger">{errorMessage(code)}</Alert>}
      </div>
    </Dialog>
  )
}
