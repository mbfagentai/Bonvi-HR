import { RefreshCw, ScanFace } from 'lucide-react'

import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Badge, Button, Card, IconTile, Skeleton } from '@/shared/ui'

import type { AttendanceDevice, DeviceEnrollmentRow } from '../api/attendance-api'
import {
  useApproveRemovals,
  useDeviceEnrollments,
  useResyncDevice,
} from '../api/attendance-queries'
import { ENROLLMENT_STATE_LOOK, linkProblemKey } from '../lib/attendance-manage-presentation'

/**
 * Qurilmaga yozish holati — qurilmalar sahifasidagi karta (SPEC-agent §F).
 * Xodim kartasidagi alohida «Qurilmalar» kartasi egasining qarori bilan
 * olib tashlandi (2026-09-27): xato bergan xodimlar shu yerda rasmi bilan
 * ko'rinadi, rasm yoki ism o'zgarsa agent o'zi qayta yozadi.
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
  const approve = useApproveRemovals()

  const page = query.data ?? null
  const summary = page?.summary ?? null
  const linkProblem = linkProblemKey(device.linkState)

  // HR allaqachon tasdiqlagan — agent keyingi tekshiruvida o'chiradi
  const approved =
    summary?.removalsApprovedUntil != null && new Date(summary.removalsApprovedUntil).getTime() > Date.now()

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
            {summary.covered > 0 && (
              <Counter label={t('attendance.enrollmentCovered')} value={num(summary.covered)} />
            )}
          </dl>

          <p className="text-caption1 text-[var(--fg-tertiary)]">
            {summary.lastContactAt === null
              ? t('attendance.enrollmentNoAgent')
              : t('attendance.enrollmentAgentSeen', { time: dateTime(summary.lastContactAt) })}
          </p>

          {/*
            Agent tirik bo'lsa ham terminal javob bermasligi mumkin — ilgari
            bu karta «hozirgina» derdi, holbuki hech kim yozilmayotgan edi.
          */}
          {linkProblem !== null && (
            <p className="text-caption1 font-medium text-ios-red">{t(linkProblem)}</p>
          )}

          {/*
            Ommaviy o'chirish to'sig'i (SPEC-agent §L): agent ro'yxat bo'sh
            bo'lsa yoki terminaldagi Bonvi odamlarining beshdan biridan ko'pi
            o'chishi kerak bo'lsa, o'chirmaydi. Haqiqatan shunday bo'lsa
            (filial yopildi) — HR shu yerda tasdiqlaydi.
          */}
          {summary.heldRemovals > 0 && (
            <Alert tone="warning" title={t('attendance.heldRemovalsTitle', { n: num(summary.heldRemovals) })}>
              {approved ? t('attendance.heldRemovalsApproved') : t('attendance.heldRemovalsText')}
            </Alert>
          )}
          {summary.heldRemovals > 0 && !approved && (
            <Button
              type="button"
              size="sm"
              variant="danger"
              disabled={approve.isPending}
              onClick={() => approve.mutate(device.id, { onSuccess: () => void query.refetch() })}
            >
              {t('attendance.heldRemovalsApprove', { n: num(summary.heldRemovals) })}
            </Button>
          )}

          {summary.foreignPersons > 0 && (
            <Alert tone="warning" title={t('attendance.foreignPersonsTitle', { n: num(summary.foreignPersons) })}>
              {t('attendance.foreignPersonsText')}
            </Alert>
          )}

          {/*
            Muammoli qatorlar — joyida turganlari ataylab ro'yxatlanmaydi
            (server ham faqat shularni yuboradi).
          */}
          {page !== null && page.rows.length > 0 && (
            <ul className="divide-y divide-[var(--border)] border-t border-[var(--border)]">
              {page.rows.map((row) => (
                <li key={row.employeeId} className="flex flex-wrap items-center gap-2.5 py-2">
                  {/* Haqiqiy rasm, bosh harflar emas — loyiha qoidasi */}
                  <EmployeePhoto
                    employeeId={row.employeeId}
                    photoKey={row.employeePhotoKey}
                    name={row.employeeName}
                    size="sm"
                  />
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
