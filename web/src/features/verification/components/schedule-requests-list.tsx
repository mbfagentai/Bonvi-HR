import { CalendarClock, Check, Pencil, Plus, SearchX, Smartphone, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { REPORTS_EXPORT } from '@/features/employees/lib/employee-presentation'
import { useListExport } from '@/features/reports/api/list-export'
import { ListExportButton, ListExportError } from '@/features/reports/components/list-export'
import { useLocale } from '@/shared/i18n/use-locale'
import { NO_DATE_RANGE, hasDateRange, type DateRange } from '@/shared/lib/list-search'
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

import {
  attendanceExportParams,
  type ScheduleChangeRequestFilters,
  type ScheduleChangeRequest,
} from '../api/attendance-api'
import { useScheduleChangeRequests } from '../api/attendance-queries'
import {
  ATTENDANCE_REQUESTS_MANAGE,
  ATTENDANCE_VIEW,
  REQUEST_STATUS_LOOK,
  SCHEDULE_KIND_LABEL_KEY,
} from '../lib/attendance-presentation'
import { isRangeInverted } from '../lib/attendance-search'
import { useScheduleRequestText } from '../lib/schedule-request-text'
import type { AbsenceAction } from './absence-decision-dialog'
import { AttendanceLoadError } from './attendance-manage-states'
import { RequestEditedNote } from './request-edited-note'
import { RequestRangeFields, RequestRangeNote } from './request-range-fields'
import { ScheduleDecisionDialog } from './schedule-decision-dialog'
import { ScheduleRequestDialog } from './schedule-request-dialog'

const helper = dataColumnHelper<ScheduleChangeRequest>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzardi. */
const NO_ROWS: ScheduleChangeRequest[] = []

const PAGE_SIZE = 25

/**
 * Katakda shuncha sana — qolgani «yana N ta», to'liq ro'yxat sichqoncha
 * ostida va qaror oynasida. Bitta: qator bir chiziq (egasi 2026-10-22).
 */
const VISIBLE_DAYS = 1

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
  range = NO_DATE_RANGE,
  onRangeChange,
}: {
  /** Faqat shu xodimning so'rovlari — xodim kartasida. */
  employeeId?: string | null
  /** Xodim ustuni ko'rsatilsinmi — umumiy navbatda. */
  showEmployee?: boolean
  /**
   * So'rov KUNLARI oralig'i — umumiy navbatda, manzildan (sahifa egasi):
   * birorta kuni (almashtirishda — ikkala sanadan biri) oraliq ichiga
   * tushsa, so'rov chiqadi (server qoidasi).
   */
  range?: DateRange
  /** Berilsa — jadvallar ustida sana filtri chiziladi. */
  onRangeChange?: (next: DateRange) => void
}) {
  const { t, date } = useLocale()
  const { hasPermission } = useAuth()
  const text = useScheduleRequestText()

  const canManage = hasPermission(ATTENDANCE_REQUESTS_MANAGE)
  /*
    Excelga yuklash — faqat umumiy navbatda (sana filtri kartasi bor joyda),
    ro'yxatni ko'rish VA `reports.export` bilan (`AbsenceRequestsList` dagi qoida).
  */
  const canExport =
    onRangeChange !== undefined && hasPermission(ATTENDANCE_VIEW) && hasPermission(REPORTS_EXPORT)

  const [pendingPage, setPendingPage] = useState(1)
  const [decidedPage, setDecidedPage] = useState(1)
  const [editing, setEditing] = useState<ScheduleChangeRequest | null>(null)
  const [adding, setAdding] = useState(false)
  const [decision, setDecision] = useState<{
    action: AbsenceAction
    request: ScheduleChangeRequest
  } | null>(null)

  /** Oraliq o'zgarsa ikkala sahifa boshidan (`AbsenceRequestsList` dagi qoida). */
  const changeRange = (next: DateRange) => {
    onRangeChange?.(next)
    setPendingPage(1)
    setDecidedPage(1)
  }

  const filtered = hasDateRange(range)

  // Teskari oraliq bilan so'rov YUBORILMAYDI (`AbsenceRequestsList` izohi)
  const rangeInvalid = isRangeInverted(range.from, range.to)
  const from = range.from === '' ? undefined : range.from
  const to = range.to === '' ? undefined : range.to

  /*
    Ikkala jadvalga umumiy filtr — holatsiz va sahifasiz. Excel fayli AYNAN
    shundan: kutayotganlar ham, qaror qabul qilinganlar ham
    (`AbsenceRequestsList` dagi eksport izohi).
  */
  const query: ScheduleChangeRequestFilters = {
    employeeId: employeeId ?? undefined,
    from,
    to,
  }

  const exportList = useListExport()

  const pending = useScheduleChangeRequests(
    {
      ...query,
      status: 'Pending',
      page: pendingPage,
      pageSize: PAGE_SIZE,
    },
    !rangeInvalid,
  )

  // «Qaror qabul qilingan» — uchta holat birga (`AbsenceRequestsList` izohi)
  const decided = useScheduleChangeRequests(
    {
      ...query,
      decided: true,
      page: decidedPage,
      pageSize: PAGE_SIZE,
    },
    !rangeInvalid,
  )

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

    /*
      ⭐ BIR QATOR (egasi 2026-10-22) — `AbsenceRequestsList` dagi qoida:
      bo'lim o'z ustunida, izohlar bir chiziqda, «ilovadan» — telefon
      belgisi, sanalardan birinchisi va «yana N ta».
    */
    if (showEmployee) {
      list.push(
        helper.display({
          id: 'employee',
          header: t('attendance.requestEmployee'),
          cell: ({ row }) => (
            <span className="flex items-center gap-2.5">
              <EmployeePhoto
                employeeId={row.original.employeeId}
                photoKey={row.original.employeePhotoKey}
                name={row.original.employeeName}
                size="sm"
              />
              <span className="font-medium">{row.original.employeeName}</span>
            </span>
          ),
        }),

        helper.accessor('departmentName', {
          header: t('employee.department'),
          enableSorting: false,
          meta: { maxWidth: '260px', hideOnMobile: true },
          cell: ({ getValue }) => <PlainCell value={getValue()} />,
        }),
      )
    }

    list.push(
      helper.accessor('kind', {
        header: t('attendance.scheduleRequestKind'),
        enableSorting: false,
        cell: ({ row }) => t(SCHEDULE_KIND_LABEL_KEY[row.original.kind]),
      }),

      helper.display({
        id: 'days',
        header: t('attendance.scheduleRequestDays'),
        cell: ({ row }) => <DaysCell lines={text.lines(row.original)} />,
      }),

      helper.accessor('note', {
        header: t('attendance.requestNote'),
        enableSorting: false,
        meta: { hideOnMobile: true },
        cell: ({ row }) => (
          <span className="flex items-center gap-2">
            <NoteText value={row.original.note} />
            {row.original.submittedByEmployee && (
              <span
                role="img"
                title={t('attendance.requestFromApp')}
                aria-label={t('attendance.requestFromApp')}
                className="text-[var(--fg-tertiary)]"
              >
                <Smartphone className="size-3.5" strokeWidth={2} />
              </span>
            )}
            {/* Yuborilgandan keyin tahrirlangan (`AbsenceRequestsList` dagi belgi) */}
            <RequestEditedNote request={row.original} />
          </span>
        ),
      }),

      helper.display({
        id: 'decision',
        header: t('attendance.requestDecisionNote'),
        meta: { hideOnMobile: true },
        cell: ({ row }) => (
          <span className="flex items-center gap-2">
            {/* Izohsiz qarorda chiziqcha yo'q — kim va qachon yozuvining o'zi yetadi */}
            {(row.original.decisionNote !== null && row.original.decisionNote !== '') ||
            row.original.decidedByName === null ? (
              <NoteText value={row.original.decisionNote} />
            ) : null}
            {row.original.decidedByName !== null && (
              <span className="text-caption2 text-[var(--fg-tertiary)]">
                {row.original.decidedByName}
                {row.original.decidedAt !== null && ` · ${date(row.original.decidedAt)}`}
              </span>
            )}
            {row.original.cancelledByEmployee && (
              <span className="text-caption2 text-[var(--fg-tertiary)]">
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
          meta: { align: 'end' },
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

  /*
    Oraliqqa mos so'rov yo'qligi — «so'rov yo'q» emas (`AbsenceRequestsList`
    dagi sabab): boshqa matn va oraliqni tozalash tugmasi.
  */
  const filteredEmpty = (
    <EmptyState
      icon={SearchX}
      title={t('attendance.requestsFilteredEmpty')}
      description={t('common.dateRangeEmptyText')}
      action={
        <Button type="button" size="sm" variant="secondary" onClick={() => changeRange(NO_DATE_RANGE)}>
          <X />
          {t('employees.clearFilters')}
        </Button>
      }
    />
  )

  return (
    <div className="space-y-4">
      {/* Sana filtri — yo'qlik navbatidagi filtr paneli bilan bir xil ko'rinish */}
      {onRangeChange !== undefined && (
        <Card className="px-4 py-3.5 sm:px-5">
          <div className="grid items-end gap-3 sm:grid-cols-[minmax(0,340px)_auto]">
            <RequestRangeFields range={range} onChange={changeRange} />

            {/* Tozalash va eksport — qatorning oxirida, bitta katakda */}
            {(filtered || canExport) && (
              <div className="flex flex-wrap items-center gap-2 sm:justify-start">
                {filtered && (
                  <Button type="button" variant="ghost" onClick={() => changeRange(NO_DATE_RANGE)}>
                    <X />
                    {t('employees.clearFilters')}
                  </Button>
                )}

                {canExport && (
                  <ListExportButton
                    onClick={() =>
                      exportList.mutate({
                        path: '/api/attendance/schedule-requests/export',
                        params: attendanceExportParams(query),
                        slug: 'jadval_sorovlari',
                      })
                    }
                    pending={exportList.isPending}
                    disabled={rangeInvalid}
                  />
                )}
              </div>
            )}
          </div>

          <RequestRangeNote range={range} />

          <ListExportError error={exportList.error} className="mt-3.5" />
        </Card>
      )}

      {!rangeInvalid && (
        <>
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
                    filtered ? (
                      filteredEmpty
                    ) : (
                      <EmptyState
                        icon={CalendarClock}
                        title={t('attendance.requestsQueueEmpty')}
                        description={t('attendance.scheduleRequestsQueueEmptyText')}
                      />
                    )
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
                    filtered ? (
                      filteredEmpty
                    ) : (
                      <EmptyState
                        icon={CalendarClock}
                        title={t('attendance.requestsEmpty')}
                        description={t('attendance.scheduleRequestsEmptyText')}
                      />
                    )
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
        </>
      )}

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
 * Sanalar katagi, BIR QATORDA: «16.10 · 08:30–18:00, tanaffus 12:23–14:23»,
 * «17.10 · Dam olish kuni», «15.10 ⇄ 18.10». Ko'p bo'lsa — birinchisi va
 * yonida «yana N ta»; to'liq ro'yxat sichqoncha ostida (`title`) va qaror
 * oynasida.
 */
function DaysCell({ lines }: { lines: readonly string[] }) {
  const { t, num } = useLocale()

  const shown = lines.slice(0, VISIBLE_DAYS)
  const rest = lines.length - shown.length

  return (
    <span className="flex items-center gap-2" title={lines.join('\n')}>
      {shown.map((line, index) => (
        <span key={index} className="tabular">
          {line}
        </span>
      ))}
      {rest > 0 && (
        <span className="text-caption2 text-[var(--fg-tertiary)]">
          {t('attendance.scheduleRequestMoreDays', { n: num(rest) })}
        </span>
      )}
    </span>
  )
}

/** Bo'sh katak chiziqcha bilan — «yuklanmadimi?» degan savol tug'ilmasin. */
function PlainCell({ value }: { value: string | null }) {
  if (value === null || value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="text-[var(--fg-secondary)]">{value}</span>
}

/** Erkin matn — bir qatorda, uzun bo'lsa kesiladi, to'liq matn `title` da. */
function NoteText({ value }: { value: string | null }) {
  if (value === null || value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return (
    <span className="block max-w-[220px] truncate text-[var(--fg-secondary)]" title={value}>
      {value}
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
