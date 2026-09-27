import { Check, Inbox, Pencil, Plus, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import {
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  Pagination,
  Skeleton,
  dataColumnHelper,
  type BadgeTone,
  type DataColumn,
} from '@/shared/ui'

import type { AbsenceRequest, AbsenceRequestStatus } from '../api/attendance-api'
import { useAbsenceRequests } from '../api/attendance-queries'
import { ATTENDANCE_REQUESTS_MANAGE } from '../lib/attendance-presentation'
import { AbsenceDecisionDialog, type AbsenceAction } from './absence-decision-dialog'
import { AbsenceRequestDialog } from './absence-request-dialog'
import { AttendanceLoadError } from './attendance-manage-states'

const helper = dataColumnHelper<AbsenceRequest>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzardi. */
const NO_ROWS: AbsenceRequest[] = []

const PAGE_SIZE = 25

/** Holat nishonining ohangi va yorlig'i — bitta joyda. */
const STATUS_LOOK: Readonly<Record<AbsenceRequestStatus, { tone: BadgeTone; labelKey: string }>> = {
  Pending: { tone: 'warning', labelKey: 'attendance.requestStatusPending' },
  Approved: { tone: 'info', labelKey: 'attendance.requestStatusApproved' },
  Rejected: { tone: 'danger', labelKey: 'attendance.requestStatusRejected' },
  Cancelled: { tone: 'neutral', labelKey: 'attendance.requestStatusCancelled' },
}

/**
 * Yo'qlik so'rovlari — IKKITA jadval: qaror kutayotganlar va qaror qabul
 * qilinganlar (SPEC-so'rovlar §B5, Verifix'ning «Запросы на отсутствие»
 * ekranidagi kabi).
 *
 * <b>Nega ikkita jadval, bitta ustunli filtr emas.</b> Bu ikki xil ish:
 * yuqoridagisi HR dan QAROR kutadi va u har kuni tozalanishi kerak;
 * pastdagisi — arxiv, unga faqat «nega bu kun sababli edi» degan savol
 * bilan qaraladi. Bitta jadvalda ular aralashib, kutayotgani ko'rinmay
 * qolardi.
 *
 * Bitta komponent ikki ekranga xizmat qiladi: xodim kartasida
 * (<c>employeeId</c> bilan) va umumiy navbatda (usiz) — server ham bitta
 * endpoint bilan javob beradi.
 */
export function AbsenceRequestsList({
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

  const canManage = hasPermission(ATTENDANCE_REQUESTS_MANAGE)

  const [pendingPage, setPendingPage] = useState(1)
  const [decidedPage, setDecidedPage] = useState(1)
  const [editing, setEditing] = useState<AbsenceRequest | null>(null)
  const [adding, setAdding] = useState(false)

  /*
    Qaror — TASDIQLASH OYNASI orqali (egasi, 2026-09-19): brauzerning
    `confirm()` i tizimning o'z oynasi bo'lib, tarjima qilinmasdi va
    ichiga izoh maydonini qo'yib bo'lmasdi. Bitta holatda ikkalasi ham
    saqlanadi — amal va qaysi qator ustida.
  */
  const [decision, setDecision] = useState<{ action: AbsenceAction; request: AbsenceRequest } | null>(
    null,
  )

  const pending = useAbsenceRequests({
    employeeId: employeeId ?? undefined,
    status: 'Pending',
    page: pendingPage,
    pageSize: PAGE_SIZE,
  })

  /*
    «Qaror qabul qilingan» — uchta holat birga (tasdiqlangan, rad etilgan,
    bekor qilingan), shu sababli `status` emas, `decided` filtri. Klientda
    filtrlash sahifalashni yolg'on qilardi: «jami» ichida kutayotganlari
    ham sanalib, oxirgi sahifa bo'sh chiqardi.
  */
  const decided = useAbsenceRequests({
    employeeId: employeeId ?? undefined,
    decided: true,
    page: decidedPage,
    pageSize: PAGE_SIZE,
  })

  /*
    Sahifa bo'shab qolsa — oxirgi MAVJUD sahifaga qaytiladi
    (`attendance-mapping-page.tsx` naqshi). 2-sahifadagi yagona so'rov
    tasdiqlangach navbat «bo'sh» deb ko'rinardi, garchi 1-sahifada yana
    25 tasi qaror kutsa. Sakrash `total` dan: bir martada to'g'ri sahifaga.
  */
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

  const columns = useMemo<Array<DataColumn<AbsenceRequest>>>(() => {
    const list: Array<DataColumn<AbsenceRequest>> = []

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
                {row.original.positionName !== null && (
                  <span className="truncate-1 block text-caption2 text-[var(--fg-tertiary)]">
                    {row.original.positionName}
                  </span>
                )}
              </span>
            </span>
          ),
        }),
      )
    }

    list.push(
      helper.accessor('absenceTypeName', {
        header: t('attendance.requestKind'),
        enableSorting: false,
        meta: { minWidth: '160px' },
        cell: ({ getValue }) => <span className="truncate-1 block">{getValue()}</span>,
      }),

      helper.display({
        id: 'period',
        header: t('attendance.requestPeriod'),
        meta: { width: '200px' },
        cell: ({ row }) => <PeriodCell request={row.original} />,
      }),

      helper.accessor('note', {
        header: t('attendance.requestNote'),
        enableSorting: false,
        meta: { minWidth: '200px', hideOnMobile: true },
        cell: ({ getValue }) => (
          <span className="truncate-2 block text-[var(--fg-secondary)]">{getValue() ?? '—'}</span>
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
          </span>
        ),
      }),

      helper.accessor('status', {
        header: t('attendance.statsColStatus'),
        enableSorting: false,
        meta: { width: '148px' },
        cell: ({ row }) => {
          const look = STATUS_LOOK[row.original.status]
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
  }, [t, date, showEmployee, canManage])

  return (
    <div className="space-y-4">
      <Card className="overflow-hidden">
        <div className="flex items-center justify-between gap-3 px-4 py-3.5 sm:px-5">
          <h2 className="text-subhead font-semibold">{t('attendance.requestsPending')}</h2>

          {canManage && employeeId !== null && (
            <Button type="button" size="sm" variant="secondary" onClick={() => setAdding(true)}>
              <Plus />
              {t('attendance.requestAdd')}
            </Button>
          )}
        </div>

        {/*
          ⚠️ Yuklab bo'lmagani «qaror kutayotgan so'rov yo'q» bo'lib
          KO'RINMAYDI: HR bo'sh navbatni ko'rib, kutayotgan so'rovlarni
          tekshirmay qo'yardi (`AttendanceLoadError` izohi).
        */}
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
                  icon={Inbox}
                  title={t('attendance.requestsQueueEmpty')}
                  description={t('attendance.requestsQueueEmptyText')}
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
                  icon={Inbox}
                  title={t('attendance.requestsEmpty')}
                  description={t('attendance.requestsEmptyText')}
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

      <AbsenceDecisionDialog
        action={decision?.action ?? null}
        request={decision?.request ?? null}
        onClose={() => setDecision(null)}
      />

      <AbsenceRequestDialog
        open={adding || editing !== null}
        employeeId={employeeId}
        request={editing}
        onClose={() => {
          setAdding(false)
          setEditing(null)
        }}
      />
    </div>
  )
}

/** Oxirgi mavjud sahifa — hech bo'lmaganda birinchisi. */
function lastPage(total: number): number {
  return Math.max(1, Math.ceil(total / PAGE_SIZE))
}

/**
 * Davr katagi: «15.09.2026» yoki «15.09.2026, 08:30–11:57» yoki
 * «12.09.2026 – 16.09.2026».
 */
function PeriodCell({ request }: { request: AbsenceRequest }) {
  const { t, date } = useLocale()

  if (request.fromDate !== request.toDate) {
    return (
      <span className="tabular block">
        {date(request.fromDate)} – {date(request.toDate)}
      </span>
    )
  }

  return (
    <span className="block leading-tight">
      <span className="tabular block">{date(request.fromDate)}</span>
      <span className="tabular mt-0.5 block text-caption2 text-[var(--fg-tertiary)]">
        {request.fromTime === null || request.toTime === null
          ? t('attendance.requestWholeDay')
          : `${request.fromTime.slice(0, 5)} – ${request.toTime.slice(0, 5)}`}
      </span>
    </span>
  )
}

/**
 * Qator amallari.
 *
 * Kutayotgan so'rovda — tasdiqlash, rad etish va tahrirlash; qaror qabul
 * qilinganida faqat bekor qilish (u kunlarni qaytaradi). Bekor qilingan
 * so'rovda umuman tugma yo'q: uni tiriltirish yo'li yo'q va bo'lmasligi
 * ham kerak — yangisini yozish aniqroq.
 */
function RowActions({
  request,
  onApprove,
  onReject,
  onEdit,
  onCancel,
}: {
  request: AbsenceRequest
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
        title={t('attendance.requestEdit')}
        aria-label={t('attendance.requestEdit')}
      >
        <Pencil />
      </Button>
    </span>
  )
}

