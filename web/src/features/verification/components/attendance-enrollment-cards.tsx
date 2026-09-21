import { Cpu, RefreshCw, ScanFace } from 'lucide-react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Badge, Button, Card, IconTile, Skeleton } from '@/shared/ui'

import type { AttendanceDevice, DeviceEnrollmentRow } from '../api/attendance-api'
import { useDeviceEnrollments, useEmployeeEnrollments, useResyncDevice, useResyncEmployee } from '../api/attendance-queries'
import { ENROLLMENT_STATE_LOOK } from '../lib/attendance-manage-presentation'
import { ATTENDANCE_MANAGE } from '../lib/attendance-presentation'

/**
 * Qurilmaga yozish holati — qurilmalar sahifasi va xodim kartasi uchun
 * ikkita karta (SPEC-agent §F).
 *
 * <b>Bu ekranlar hech qachon terminalga o'zi murojaat qilmaydi.</b> Server
 * ham qilmaydi: ishlab chiqarish serveri ofis tarmog'idagi qurilmaga yeta
 * olmaydi va yozishni LAN ichidagi agent bajaradi (SPEC-agent §C). Shu
 * sababli tugmalar «yozdim» demaydi — ular faqat BELGINI eskirgan qiladi,
 * agent esa keyingi tekshiruvida (60 soniyagacha) yozadi va hisobot
 * yuboradi. Ekrandagi «Yozildi» faqat qurilmaning O'ZI tasdiqlagan yozuv.
 *
 * ⚠️ «Hammasini sinxronlash» — qurilmadagi odamlarni o'chirib qayta yozish
 * EMAS (egasi, 2026-09-21). U xato qatorlarni qayta urinishga qo'yadi;
 * yetishmagan odamni agent tugmasiz ham yozadi, joyida turganiga esa
 * umuman tegilmaydi.
 */
export function DeviceEnrollmentCard({ device }: { device: AttendanceDevice }) {
  const { t, num, dateTime } = useLocale()

  const query = useDeviceEnrollments(device.id, device.isActive)
  const resync = useResyncDevice()

  const page = query.data ?? null
  const summary = page?.summary ?? null

  return (
    <Card className="space-y-3.5 p-4">
      <div className="flex flex-wrap items-center gap-2.5">
        <IconTile icon={ScanFace} tone="blue" size="sm" />
        <div className="min-w-0 flex-1">
          <h2 className="truncate-1 text-subhead font-semibold">{t('attendance.enrollmentTitle')}</h2>
          <p className="truncate-1 text-caption1 text-[var(--fg-tertiary)]">{device.name}</p>
        </div>

        <Button
          type="button"
          size="sm"
          variant="soft"
          pill
          disabled={resync.isPending || query.isPending}
          onClick={() =>
            resync.mutate(device.id, {
              onSuccess: () => void query.refetch(),
            })
          }
        >
          <RefreshCw className={cn(resync.isPending && 'animate-spin')} />
          {t('attendance.enrollmentResyncAll')}
        </Button>
      </div>

      {query.isPending ? (
        <Skeleton className="h-16 w-full" />
      ) : query.isError || summary === null ? (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {t('dashboard.loadFailedHint')}
        </Alert>
      ) : (
        <>
          <dl className="flex flex-wrap gap-x-5 gap-y-2 text-footnote">
            <Counter label={t('attendance.enrollmentTotal')} value={num(summary.total)} />
            <Counter
              label={t('attendance.enrollmentSynced')}
              value={num(summary.synced)}
              className="text-ios-green"
            />
            <Counter label={t('attendance.enrollmentPending')} value={num(summary.pending)} />
            <Counter
              label={t('attendance.enrollmentFailed')}
              value={num(summary.failed)}
              className={summary.failed > 0 ? 'text-ios-red' : undefined}
            />
            <Counter
              label={t('attendance.enrollmentPhotoMissing')}
              value={num(summary.photoMissing)}
              className={summary.photoMissing > 0 ? 'text-ios-orange' : undefined}
            />
          </dl>

          <p className="text-caption1 text-[var(--fg-tertiary)]">
            {summary.lastContactAt === null
              ? t('attendance.enrollmentNoAgent')
              : t('attendance.enrollmentAgentSeen', { time: dateTime(summary.lastContactAt) })}
          </p>

          {/*
            Muammoli qatorlar — joyida turganlari ataylab ro'yxatlanmaydi
            (server ham faqat shularni yuboradi).
          */}
          {page !== null && page.rows.length > 0 && (
            <ul className="divide-y divide-[var(--border)] border-t border-[var(--border)]">
              {page.rows.map((row) => (
                <li key={row.employeeId} className="flex flex-wrap items-center gap-2 py-2">
                  <span className="min-w-0 flex-1 truncate-1 text-footnote">{row.employeeName}</span>
                  <EnrollmentBadge row={row} />
                </li>
              ))}
            </ul>
          )}

          {resync.isSuccess && (
            <p className="text-caption1 text-[var(--fg-secondary)]">
              {resync.data.affected === 0
                ? t('attendance.enrollmentResyncNone')
                : t('attendance.enrollmentResynced', { n: num(resync.data.affected) })}
            </p>
          )}
        </>
      )}
    </Card>
  )
}

/** Xodim kartasidagi «Qurilmalar» qatori — Davomat tabining ostida. */
export function EmployeeEnrollmentsCard({ employeeId }: { employeeId: string }) {
  const { t, num, dateTime } = useLocale()
  const { hasPermission } = useAuth()

  const canManage = hasPermission(ATTENDANCE_MANAGE)

  const query = useEmployeeEnrollments(employeeId)
  const resync = useResyncEmployee()

  const rows = query.data ?? []

  /*
    Faol qurilma umuman bo'lmasa, karta CHIZILMAYDI: davomat moduli
    ishlatilmayotgan korxonada bu qator faqat savol tug'dirardi.
  */
  if (!query.isPending && rows.length === 0) {
    return null
  }

  return (
    <Card className="space-y-3 p-4">
      <div className="flex flex-wrap items-center gap-2.5">
        <IconTile icon={Cpu} tone="teal" size="sm" />
        <h2 className="min-w-0 flex-1 truncate-1 text-subhead font-semibold">
          {t('attendance.enrollmentDevices')}
        </h2>

        {canManage && (
          <Button
            type="button"
            size="sm"
            variant="soft"
            pill
            disabled={resync.isPending || query.isPending}
            onClick={() =>
              resync.mutate(employeeId, {
                onSuccess: () => void query.refetch(),
              })
            }
          >
            <RefreshCw className={cn(resync.isPending && 'animate-spin')} />
            {t('attendance.enrollmentResyncOne')}
          </Button>
        )}
      </div>

      {query.isPending ? (
        <Skeleton className="h-10 w-full" />
      ) : query.isError ? (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {t('dashboard.loadFailedHint')}
        </Alert>
      ) : (
        <ul className="divide-y divide-[var(--border)]">
          {rows.map((row) => (
            <li key={row.deviceId} className="flex flex-wrap items-center gap-2 py-2 first:pt-0">
              <div className="min-w-0 flex-1">
                <p className="truncate-1 text-footnote">{row.deviceName}</p>
                <p className="tabular truncate-1 font-mono text-caption2 text-[var(--fg-tertiary)]">
                  {row.personNo ?? '—'}
                  {row.syncedAt !== null && ` · ${dateTime(row.syncedAt)}`}
                </p>
              </div>
              <EnrollmentBadge row={row} />
            </li>
          ))}
        </ul>
      )}

      <p className="text-caption1 text-[var(--fg-tertiary)]">{t('attendance.enrollmentHint')}</p>

      {resync.isSuccess && (
        <p className="text-caption1 text-[var(--fg-secondary)]">
          {t('attendance.enrollmentResynced', { n: num(resync.data.affected) })}
        </p>
      )}
    </Card>
  )
}

/**
 * Holat nishoni.
 *
 * Qurilmaning O'Z xato matni nishonning `title` ida: u ko'pincha
 * inglizcha va texnik («face quality too low»), lekin AYNAN shu matn HR
 * ga nima qilishni aytadi — shu sababli tarjima qilinmaydi va
 * qisqartirilmaydi.
 */
function EnrollmentBadge({ row }: { row: DeviceEnrollmentRow }) {
  const { t } = useLocale()

  const look = ENROLLMENT_STATE_LOOK[row.state]

  return (
    <Badge tone={look.tone} size="sm" title={row.error ?? undefined}>
      {t(look.labelKey)}
    </Badge>
  )
}

function Counter({
  label,
  value,
  className,
}: {
  label: string
  value: string
  className?: string
}) {
  return (
    <div className="flex items-baseline gap-1.5">
      <dt className="text-[var(--fg-tertiary)]">{label}</dt>
      <dd className={cn('tabular font-semibold', className)}>{value}</dd>
    </div>
  )
}
