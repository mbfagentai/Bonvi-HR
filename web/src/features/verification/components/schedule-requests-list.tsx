import { CalendarClock, Check, Pencil, Plus, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  Pagination,
  Skeleton,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type { ScheduleChangeRequest } from '../api/attendance-api'
import { useScheduleChangeRequests } from '../api/attendance-queries'
import {
  ATTENDANCE_REQUESTS_MANAGE,
  REQUEST_STATUS_LOOK,
  SCHEDULE_KIND_LABEL_KEY,
} from '../lib/attendance-presentation'
import { useScheduleRequestText } from '../lib/schedule-request-text'
import type { AbsenceAction } from './absence-decision-dialog'
import { AttendanceLoadError } from './attendance-manage-states'
import { ScheduleDecisionDialog } from './schedule-decision-dialog'
import { ScheduleRequestDialog } from './schedule-request-dialog'

const helper = dataColumnHelper<ScheduleChangeRequest>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzardi. */
const NO_ROWS: ScheduleChangeRequest[] = []

const PAGE_SIZE = 25

/** Katakda shuncha sana — qolgani «yana N ta», to'liq ro'yxat qaror oynasida. */
const VISIBLE_DAYS = 3

/**
 * Jadval so'rovlari — `AbsenceRequestsList` ning egizagi (SPEC-jadval-sorovi
 * §C4): yuqorida qaror kutayotganlar, pastda qaror qabul qilinganlar.
 *
 * Nega alohida komponent, umumiy jadval emas: ustunlar boshqa (yo'qlik turi va
 * davr o'rniga — so'rov turi va sanalar ro'yxati), qaror qoidasi va tugmalar esa
 * AYNAN bir xil — holat nishoni ham bitta xaritadan (`REQUEST_STATUS_LOOK`).
 * Ikki xil so'rovni bitta jadvalga qorishtirish har ustunni «bu qatorda nima
 * bor?» degan shartga aylantirardi.
 */
export function ScheduleRequestsList({
  employeeId = null,
  showEmployee = false,
}: {
  /** Faqat shu xodimning so'rovlari — xodim kartasida. */
  employeeId?: string | null
  /** Xodim ustuni ko'rsatilsinmi — umumiy navbatda. */
  showEmployee?: boolean
}) {
  const { t, date } = useLocale()
  const { hasPermission } = useAuth()
  const text = useScheduleRequestText()

  const canManage = hasPermission(ATTENDANCE_REQUESTS_MANAGE)

  const [pendingPage, setPendingPage] = useState(1)
  const [decidedPage, setDecidedPage] = useState(1)
  const [editing, setEditing] = useState<ScheduleChangeRequest | null>(null)
  const [adding, setAdding] = useState(false)
  const [decision, setDecision] = useState<{
    action: AbsenceAction
    request: ScheduleChangeRequest
  } | null>(null)

  const pending = useScheduleChangeRequests({
    employeeId: employeeId ?? undefined,
    status: 'Pending',
    page: pendingPage,
    pageSize: PAGE_SIZE,
  })

  // «Qaror qabul qilingan» — uchta holat birga (`AbsenceRequestsList` izohi)
  const decided = useScheduleChangeRequests({
    employeeId: employeeId ?? undefined,
    decided: true,
    page: decidedPage,
    pageSize: PAGE_SIZE,
  })

  // Sahifa bo'shab qolsa — oxirgi MAVJUD sahifaga (`AbsenceRequestsList` naqshi)
  const pendingItems = pending.data?.items
  const pendingTotal = pending.data?.total
  useEffect(() => {
    if (pendingItems !== undefined && pendingItems.length === 0 && pendingPage > 1) {
      setPendingPage(lastPage(pendingTotal ?? 0))
    }
  }, [pendingItems, pendingTotal, pendingPage])

  const decidedItems = decided.data?.items
  const decidedTotal = decided.data?.total
  useEffect(() => {
    if (decidedItems !== undefined && decidedItems.length === 0 && decidedPage > 1) {
      setDecidedPage(lastPage(decidedTotal ?? 0))
    }
  }, [decidedItems, decidedTotal, decidedPage])

  const columns = useMemo<Array<DataColumn<ScheduleChangeRequest>>>(() => {
    const list: Array<DataColumn<ScheduleChangeRequest>> = []

    if (showEmployee) {
      list.push(
        helper.display({
          id: 'employee',
          header: t('attendance.requestEmployee'),
          meta: { minWidth: '220px' },
          cell: ({ row }) => (
            <span className="flex min-w-0 items-center gap-2.5">
              <EmployeePhoto
                employeeId={row.original.employeeId}
                photoKey={row.original.employeePhotoKey}
                name={row.original.employeeName}
                size="sm"
              />
              <span className="min-w-0">
                <span className="truncate-1 block font-medium">{row.original.employeeName}</span>
                {(row.original.departmentName ?? row.original.positionName) !== null && (
                  <span className="truncate-1 block text-caption2 text-[var(--fg-tertiary)]">
                    {row.original.departmentName ?? row.original.positionName}
                  </span>
                )}
              </span>
            </span>
          ),
        }),
      )
    }

    list.push(
      helper.accessor('kind', {
        header: t('attendance.scheduleRequestKind'),
        enableSorting: false,
        meta: { width: '168px' },
        cell: ({ row }) => (
          <span className="truncate-1 block">{t(SCHEDULE_KIND_LABEL_KEY[row.original.kind])}</span>
        ),
      }),

      helper.display({
        id: 'days',
        header: t('attendance.scheduleRequestDays'),
        meta: { minWidth: '260px' },
        cell: ({ row }) => <DaysCell lines={text.lines(row.original)} />,
      }),

      helper.accessor('note', {
        header: t('attendance.requestNote'),
        enableSorting: false,
        meta: { minWidth: '200px', hideOnMobile: true },
        cell: ({ row }) => (
          <span className="block leading-tight">
            <span className="truncate-2 block text-[var(--fg-secondary)]">
              {row.original.note ?? '—'}
            </span>
            {row.original.submittedByEmployee && (
              <span className="mt-0.5 block text-caption2 text-[var(--fg-tertiary)]">
                {t('attendance.requestFromApp')}
              </span>
            )}
          </span>
        ),
      }),

      helper.display({
        id: 'decision',
        header: t('attendance.requestDecisionNote'),
        meta: { minWidth: '180px', hideOnMobile: true },
        cell: ({ row }) => (
          <span className="block leading-tight">
            <span className="truncate-2 block text-[var(--fg-secondary)]">
              {row.original.decisionNote ?? '—'}
            </span>
            {row.original.decidedByName !== null && (
              <span className="truncate-1 mt-0.5 block text-caption2 text-[var(--fg-tertiary)]">
                {row.original.decidedByName}
                {row.original.decidedAt !== null && ` · ${date(row.original.decidedAt)}`}
              </span>
            )}
            {row.original.cancelledByEmployee && (
              <span className="truncate-1 mt-0.5 block text-caption2 text-[var(--fg-tertiary)]">
                {t('attendance.requestCancelledByEmployee')}
                {row.original.decidedAt !== null && ` · ${date(row.original.decidedAt)}`}
              </span>
            )}
          </span>
        ),
      }),

      helper.accessor('status', {
        header: t('attendance.statsColStatus'),
        enableSorting: false,
        meta: { width: '148px' },
        cell: ({ row }) => {
          const look = REQUEST_STATUS_LOOK[row.original.status]
          return <Badge tone={look.tone}>{t(look.labelKey)}</Badge>
        },
      }),
    )

    if (canManage) {
      list.push(
        helper.display({
          id: 'actions',
          header: '',
          meta: { width: '180px' },
          cell: ({ row }) => (
            <RowActions
              request={row.original}
              onApprove={() => setDecision({ action: 'approve', request: row.original })}
              onReject={() => setDecision({ action: 'reject', request: row.original })}
              onEdit={() => setEditing(row.original)}
              onCancel={() => setDecision({ action: 'cancel', request: row.original })}
            />
          ),
        }),
      )
    }

    return list
  }, [t, date, text, showEmployee, canManage])

  return (
    <div className="space-y-4">
      <Card className="overflow-hidden">
        <div className="flex items-center justify-between gap-3 px-4 py-3.5 sm:px-5">
          <h2 className="text-subhead font-semibold">{t('attendance.requestsPending')}</h2>

          {canManage && employeeId !== null && (
            <Button type="button" size="sm" variant="secondary" onClick={() => setAdding(true)}>
              <Plus />
              {t('attendance.scheduleRequestAdd')}
            </Button>
          )}
        </div>

        {/* Yuklab bo'lmagani «so'rov yo'q» bo'lib ko'rinmaydi (`AttendanceLoadError` izohi) */}
        {pending.isError ? (
          <AttendanceLoadError
            error={pending.error}
            onRetry={() => void pending.refetch()}
            retrying={pending.isFetching}
          />
        ) : (
          <>
            <DataTable
              columns={columns}
              data={pending.isPending ? NO_ROWS : (pending.data?.items ?? NO_ROWS)}
              getRowId={(row) => row.id}
              loading={pending.isPending}
              skeletonRows={3}
              rowLabel={(row) => row.employeeName}
              empty={
                <EmptyState
                  icon={CalendarClock}
                  title={t('attendance.requestsQueueEmpty')}
                  description={t('attendance.scheduleRequestsQueueEmptyText')}
                />
              }
            />

            <Pagination
              page={pendingPage}
              pageSize={PAGE_SIZE}
              total={pending.data?.total ?? 0}
              onPageChange={setPendingPage}
              className="px-4 pb-4 sm:px-5"
            />
          </>
        )}
      </Card>

      <Card className="overflow-hidden">
        <div className="px-4 py-3.5 sm:px-5">
          <h2 className="text-subhead font-semibold">{t('attendance.requestsDecided')}</h2>
        </div>

        {decided.isPending ? (
          <div aria-hidden className="space-y-3 px-4 pb-5 sm:px-5">
            {[0, 1, 2].map((row) => (
              <Skeleton key={row} className="h-10" />
            ))}
          </div>
        ) : decided.isError ? (
          <AttendanceLoadError
            error={decided.error}
            onRetry={() => void decided.refetch()}
            retrying={decided.isFetching}
          />
        ) : (
          <>
            <DataTable
              columns={columns}
              data={decided.data?.items ?? NO_ROWS}
              getRowId={(row) => row.id}
              rowLabel={(row) => row.employeeName}
              empty={
                <EmptyState
                  icon={CalendarClock}
                  title={t('attendance.requestsEmpty')}
                  description={t('attendance.scheduleRequestsEmptyText')}
                />
              }
            />

            <Pagination
              page={decidedPage}
              pageSize={PAGE_SIZE}
              total={decided.data?.total ?? 0}
              onPageChange={setDecidedPage}
              className="px-4 pb-4 sm:px-5"
            />
          </>
        )}
      </Card>

      <ScheduleDecisionDialog
        action={decision?.action ?? null}
        request={decision?.request ?? null}
        onClose={() => setDecision(null)}
      />

      {canManage && (
        <ScheduleRequestDialog
          open={adding || editing !== null}
          employeeId={employeeId}
          request={editing}
          onClose={() => {
            setAdding(false)
            setEditing(null)
          }}
        />
      )}
    </div>
  )
}

/** Oxirgi mavjud sahifa — hech bo'lmaganda birinchisi. */
function lastPage(total: number): number {
  return Math.max(1, Math.ceil(total / PAGE_SIZE))
}

/**
 * Sanalar katagi: «16.10 · 08:30–18:00, tanaffus 12:23–14:23»,
 * «17.10 · Dam olish kuni», «15.10 ⇄ 18.10» — har biri o'z qatorida.
 * Ko'p bo'lsa — birinchi uchtasi va «yana N ta»; to'liq ro'yxat sichqoncha
 * ostida (`title`) va qaror oynasida.
 */
function DaysCell({ lines }: { lines: readonly string[] }) {
  const { t, num } = useLocale()

  const shown = lines.slice(0, VISIBLE_DAYS)
  const rest = lines.length - shown.length

  return (
    <span className="block leading-tight" title={lines.join('\n')}>
      {shown.map((line, index) => (
        <span key={index} className={cn('tabular block', index > 0 && 'mt-0.5')}>
          {line}
        </span>
      ))}
      {rest > 0 && (
        <span className="mt-0.5 block text-caption2 text-[var(--fg-tertiary)]">
          {t('attendance.scheduleRequestMoreDays', { n: num(rest) })}
        </span>
      )}
    </span>
  )
}

/**
 * Qator amallari — `AbsenceRequestsList` dagi qoidaning o'zi: kutayotganda
 * tasdiqlash, rad etish va tahrirlash; qaror qabul qilinganda faqat bekor
 * qilish (kunlar jadvalning o'z rejasiga qaytadi); bekor qilinganda — hech narsa.
 */
function RowActions({
  request,
  onApprove,
  onReject,
  onEdit,
  onCancel,
}: {
  request: ScheduleChangeRequest
  onApprove: () => void
  onReject: () => void
  onEdit: () => void
  onCancel: () => void
}) {
  const { t } = useLocale()

  if (request.status === 'Cancelled') {
    return null
  }

  if (request.status !== 'Pending') {
    return (
      <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
        <X />
        {t('attendance.requestCancel')}
      </Button>
    )
  }

  return (
    <span className="flex items-center gap-1">
      <Button
        type="button"
        size="icon"
        variant="ghost"
        onClick={onApprove}
        title={t('attendance.requestApprove')}
        aria-label={t('attendance.requestApprove')}
      >
        <Check className="text-ios-green" />
      </Button>
      <Button
        type="button"
        size="icon"
        variant="ghost"
        onClick={onReject}
        title={t('attendance.requestReject')}
        aria-label={t('attendance.requestReject')}
      >
        <X className="text-ios-red" />
      </Button>
      <Button
        type="button"
        size="icon"
        variant="ghost"
        onClick={onEdit}
        title={t('attendance.scheduleRequestEdit')}
        aria-label={t('attendance.scheduleRequestEdit')}
      >
        <Pencil />
      </Button>
    </span>
  )
}
