import { getRouteApi, Link } from '@tanstack/react-router'
import { ChartColumn, FileSpreadsheet, LoaderCircle, SearchX } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { isRateLimited } from '@/features/employees/lib/employee-error'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  Pagination,
  SegmentedControl,
  Select,
  TextField,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type {
  AttendanceExceptionRow,
  AttendanceSummaryRow,
  ExceptionFilters,
  ExceptionKind,
  SummaryFilters,
} from '../api/attendance-api'
import {
  useAttendanceExceptions,
  useAttendanceSummary,
  useExportExceptions,
  useExportSummary,
} from '../api/attendance-queries'
import {
  ATTENDANCE_VIEW,
  DEFAULT_PAGE_SIZE,
  REPORTS_EXPORT,
  REPORT_KINDS,
  REPORT_KIND_LOOK,
  formatDuration,
  formatPassTime,
  supportsMinMinutes,
  type ReportKind,
} from '../lib/attendance-presentation'
import {
  isRangeInverted,
  isRangeTooLong,
  kindToSearch,
  monthRange,
  currentMonth,
  reportKind,
  type ReportsSearch,
} from '../lib/attendance-search'
import { DayStatusBadge } from './attendance-timesheet-grid'

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`incidents-page.tsx` dagi izoh).
 */
const route = getRouteApi('/_app/verifikatsiya/hisobotlar')

const exceptionHelper = dataColumnHelper<AttendanceExceptionRow>()
const summaryHelper = dataColumnHelper<AttendanceSummaryRow>()

const EMPTY_EXCEPTIONS: AttendanceExceptionRow[] = []
const EMPTY_SUMMARY: AttendanceSummaryRow[] = []

interface ReportFilterState {
  from: string
  to: string
  legalEntityId: string
  departmentId: string
  positionId: string
  /** Satr — `<input type="number">` qiymati; bo'sh — filtr yo'q. */
  minMinutes: string
}

/**
 * Sukut davri — joriy oyning 1-sanasidan BUGUNGACHA.
 *
 * Server ham sanasiz so'rovni aynan shunday oladi (`PeriodDefault.MonthToDate`),
 * lekin maydonlar ekranda OCHIQ to'ldiriladi: HR raqam qaysi kunlarga
 * tegishli ekanini o'qishi kerak. Funksiya, konstanta emas — sahifa yarim
 * tundan keyin ochilsa ham «bugun» to'g'ri bo'lsin.
 */
function defaultFilters(): ReportFilterState {
  return {
    from: monthRange(currentMonth()).from,
    to: todayIso(),
    legalEntityId: '',
    departmentId: '',
    positionId: '',
    minMinutes: '',
  }
}

/**
 * Davomat hisobotlari — beshta tab (SPEC §8.4).
 *
 * Birinchi to'rttasi `/reports/exceptions` ning `kind` lari (kechikkanlar,
 * erta ketganlar, kelmaganlar, muammoli kunlar), beshinchisi —
 * `/reports/summary` (xodim bo'yicha xulosa). Tab MANZILDA (`?kind=`):
 * «kechikkanlar ro'yxati» havolasini yuborish mumkin.
 *
 * Ikki so'rov ham SHARTSIZ chaqiriladi, ishlamaydiganini `enabled`
 * to'xtatadi — hook'larni shart ostida chaqirib bo'lmaydi (React qoidasi).
 */
export function AttendanceReportsPage() {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canView = hasPermission(ATTENDANCE_VIEW)
  const canExport = hasPermission(REPORTS_EXPORT)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const kind = reportKind(search)
  const page = search.page ?? 1
  const isSummary = kind === 'Summary'

  const [filters, setFilters] = useState<ReportFilterState>(defaultFilters)

  const options = useFilterOptions()

  const setSearch = useCallback(
    (
      next: (previous: ReportsSearch) => ReportsSearch,
      history: 'push' | 'replace' = 'replace',
    ) => {
      void navigate({ search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  /*
    Oraliq noto'g'ri bo'lsa so'rov YUBORILMAYDI va sabab maydonlar ostida
    aytiladi — serverning o'sha kodining tarjimasi bilan
    (`davomat.davr_notogri` / `davomat.davr_uzun`), ya'ni gap ikki joyda
    bir xil so'z bilan aytiladi.
  */
  const rangeProblem =
    filters.from === '' || filters.to === ''
      ? null
      : isRangeInverted(filters.from, filters.to)
        ? 'davomat.davr_notogri'
        : isRangeTooLong(filters.from, filters.to)
          ? 'davomat.davr_uzun'
          : null

  /**
   * So'rov filtrlari — ekrandagi jadval ham, Excel ham AYNAN shulardan.
   *
   * «Eng kam daqiqa» faqat kechikish va erta ketishda ma'noli (SPEC §6.6);
   * boshqa tabda maydon ko'rinmaydi va qiymati ham YUBORILMAYDI — yashirin
   * filtr ro'yxatni tushunarsiz qisqartirardi.
   */
  const exceptionKind: ExceptionKind = isSummary ? 'Late' : kind
  const minMinutes = parseMinutes(filters.minMinutes)

  const exceptionFilters = useMemo<ExceptionFilters>(
    () => ({
      kind: exceptionKind,
      from: emptyToUndefined(filters.from),
      to: emptyToUndefined(filters.to),
      legalEntityId: emptyToUndefined(filters.legalEntityId),
      departmentId: emptyToUndefined(filters.departmentId),
      positionId: emptyToUndefined(filters.positionId),
      minMinutes: supportsMinMinutes(exceptionKind) ? minMinutes : undefined,
    }),
    [exceptionKind, filters, minMinutes],
  )

  const summaryFilters = useMemo<SummaryFilters>(
    () => ({
      from: filters.from,
      to: filters.to,
      legalEntityId: emptyToUndefined(filters.legalEntityId),
      departmentId: emptyToUndefined(filters.departmentId),
      positionId: emptyToUndefined(filters.positionId),
    }),
    [filters],
  )

  const ready = canView && rangeProblem === null

  const exceptions = useAttendanceExceptions(
    { ...exceptionFilters, page, pageSize: DEFAULT_PAGE_SIZE },
    ready && !isSummary,
  )
  const summary = useAttendanceSummary(
    { ...summaryFilters, page, pageSize: DEFAULT_PAGE_SIZE },
    ready && isSummary,
  )

  const exportExceptions = useExportExceptions()
  const exportSummary = useExportSummary()
  const exporting = isSummary ? exportSummary : exportExceptions

  const active = isSummary ? summary : exceptions
  const total = active.data?.total ?? 0

  /*
    Oxirgi sahifadan narida qolib ketmaslik (`employees-page.tsx` dagi
    izoh). `isPlaceholderData` da eski javobning `total` iga qarab
    sakralmaydi.
  */
  useEffect(() => {
    if (active.data === undefined || active.isPlaceholderData) return

    const lastPage = Math.max(1, Math.ceil(active.data.total / DEFAULT_PAGE_SIZE))
    if (page <= lastPage) return

    setSearch((previous) => ({ ...previous, page: lastPage <= 1 ? undefined : lastPage }))
  }, [active.data, active.isPlaceholderData, page, setSearch])

  const changeKind = (next: ReportKind) => {
    /*
      `replace` — tab almashtirish ekran ichidagi ko'rinish, alohida sahifa
      emas (`reports-page.tsx` dagi davr tanlagichi bilan bir xil qoida).
      Sahifa boshiga qaytadi: boshqa ro'yxatning 4-sahifasi bo'sh bo'lishi
      mumkin.
    */
    setSearch(() => ({ kind: kindToSearch(next), page: undefined }))
    exportExceptions.reset()
    exportSummary.reset()
  }

  const changeFilters = (next: ReportFilterState) => {
    setFilters(next)
    setSearch((previous) => ({ ...previous, page: undefined }))
  }

  const set = <K extends keyof ReportFilterState>(key: K, value: ReportFilterState[K]) => {
    changeFilters({ ...filters, [key]: value })
  }

  const exceptionColumns = useExceptionColumns(exceptionKind)
  const summaryColumns = useSummaryColumns()

  const title = (
    <span className="flex items-center gap-3">
      <IconTile icon={ChartColumn} tone="brand" size="lg" />
      {t('attendance.reportsTitle')}
    </span>
  )

  if (!canView) {
    return (
      <div className="space-y-6">
        <PageHeader title={title} />
        <Card>
          <EmptyState
            icon={ChartColumn}
            title={t('attendance.noAccessTitle')}
            description={t('attendance.noAccessText')}
          />
        </Card>
      </div>
    )
  }

  const firstLoad = active.isPending && rangeProblem === null

  const empty =
    rangeProblem !== null || active.isError ? undefined : (
      <EmptyState
        icon={SearchX}
        title={t('attendance.reportEmptyTitle')}
        description={t('attendance.reportEmptyText')}
      />
    )

  return (
    <div className="space-y-6">
      <PageHeader
        title={title}
        subtitle={
          filters.from !== '' && filters.to !== ''
            ? `${t(REPORT_KIND_LOOK[kind].labelKey)} · ${date(filters.from)} – ${date(filters.to)}`
            : t(REPORT_KIND_LOOK[kind].labelKey)
        }
        actions={
          canExport ? (
            <Button
              type="button"
              // Ikki marta bosilsa ikki fayl va tezlik cheklovi bekorga yeyiladi
              disabled={exporting.isPending || rangeProblem !== null}
              onClick={() =>
                isSummary
                  ? exportSummary.mutate(summaryFilters)
                  : exportExceptions.mutate(exceptionFilters)
              }
            >
              {exporting.isPending ? <LoaderCircle className="animate-spin" /> : <FileSpreadsheet />}
              <span className="hidden sm:inline">
                {exporting.isPending ? t('employees.exporting') : t('attendance.export')}
              </span>
            </Button>
          ) : undefined
        }
      />

      {active.isError && (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {errorMessage(problemCode(active.error))}
        </Alert>
      )}

      {exporting.isError && (
        <Alert tone="danger" title={t('employees.exportFailed')}>
          {isRateLimited(exporting.error)
            ? t('employees.exportTooOften')
            : errorMessage(problemCode(exporting.error))}
        </Alert>
      )}

      <Card className="overflow-hidden">
        {/*
          Beshta tab — segmentlangan boshqaruv (iOS: o'zaro istisno
          ko'rinishlar). Tor ekranda gorizontal suriladi: beshta o'zbekcha
          yorliq telefon kengligiga sig'maydi, qisqartirish esa ma'noni
          yo'qotardi.
        */}
        <div className="overflow-x-auto border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
          <SegmentedControl
            value={kind}
            onChange={changeKind}
            aria-label={t('attendance.reportsTitle')}
            options={REPORT_KINDS.map((value) => ({
              value,
              label: t(REPORT_KIND_LOOK[value].labelKey),
            }))}
          />
        </div>

        <div className="border-b border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <TextField
              label={t('common.dateFrom')}
              type="date"
              value={filters.from}
              max={filters.to === '' ? undefined : filters.to}
              onChange={(event) => set('from', event.target.value)}
            />
            <TextField
              label={t('common.dateTo')}
              type="date"
              value={filters.to}
              min={filters.from === '' ? undefined : filters.from}
              onChange={(event) => set('to', event.target.value)}
            />
            {supportsMinMinutes(kind) && (
              <TextField
                label={t('attendance.minMinutes')}
                type="number"
                inputMode="numeric"
                min={0}
                step={1}
                value={filters.minMinutes}
                onChange={(event) => set('minMinutes', event.target.value)}
              />
            )}
            <Select
              label={t('employee.legalEntity')}
              placeholder={t('employees.anyLegalEntity')}
              options={options.legalEntities}
              value={filters.legalEntityId}
              onChange={(event) => set('legalEntityId', event.target.value)}
            />
            <Select
              label={t('employee.department')}
              placeholder={t('employees.anyDepartment')}
              options={options.departments}
              value={filters.departmentId}
              onChange={(event) => set('departmentId', event.target.value)}
            />
            <Select
              label={t('employee.position')}
              placeholder={t('employees.anyPosition')}
              options={options.positions}
              value={filters.positionId}
              onChange={(event) => set('positionId', event.target.value)}
            />
          </div>

          {rangeProblem !== null && (
            <Alert tone="warning" className="mt-3.5">
              {errorMessage(rangeProblem)}
            </Alert>
          )}
        </div>

        {isSummary ? (
          <DataTable
            columns={summaryColumns}
            data={rangeProblem === null ? (summary.data?.items ?? EMPTY_SUMMARY) : EMPTY_SUMMARY}
            getRowId={(row) => row.employeeId}
            loading={firstLoad}
            skeletonRows={8}
            empty={empty}
          />
        ) : (
          <DataTable
            columns={exceptionColumns}
            data={
              rangeProblem === null ? (exceptions.data?.items ?? EMPTY_EXCEPTIONS) : EMPTY_EXCEPTIONS
            }
            getRowId={(row) => `${row.employeeId}:${row.date}`}
            loading={firstLoad}
            skeletonRows={8}
            empty={empty}
          />
        )}

        {total > 0 && rangeProblem === null && (
          <Pagination
            page={page}
            pageSize={DEFAULT_PAGE_SIZE}
            total={total}
            onPageChange={(next) =>
              setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push')
            }
            disabled={active.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>

      {/*
        Raqamlar qaysi manbadan: hisobotlar o'tishlardan emas, HISOBLANGAN
        kunlardan tuziladi va qayta hisoblash navbati bir necha daqiqa
        orqada yuradi (SPEC §9.3). Busiz «hozirgina kechikib keldi, lekin
        ro'yxatda yo'q» degan savol nosozlik bo'lib ko'rinardi.
      */}
      <p className="text-caption1 text-[var(--fg-tertiary)]">{t('attendance.reportsSourceNote')}</p>
    </div>
  )
}

/* ═════════════════════════════════════════════════════════ ustunlar */

/**
 * Chetlanishlar jadvalining ustunlari — TABGA qarab.
 *
 * Har bir tab faqat o'z savoliga javob beradigan ustunlarni ko'rsatadi:
 * «kechikkanlar» da erta ketish ustuni, «kelmaganlar» da kirish vaqti
 * doim bo'sh bo'lardi va HR ning ko'zi bo'sh chiziqchalar orasida
 * adashardi. Server qatori baribir to'liq (§6.6), Excel ham to'liq.
 */
function useExceptionColumns(kind: ExceptionKind): Array<DataColumn<AttendanceExceptionRow>> {
  const { t, date } = useLocale()

  return useMemo(() => {
    const employee = exceptionHelper.display({
      id: 'employee',
      header: t('attendance.employee'),
      meta: { minWidth: '280px' },
      cell: ({ row }) => (
        <EmployeeCell
          employeeId={row.original.employeeId}
          name={row.original.employeeName}
          photoKey={row.original.photoKey}
          detail={[row.original.department, row.original.position].filter(Boolean).join(' · ')}
        />
      ),
    })

    const day = exceptionHelper.display({
      id: 'date',
      header: t('attendance.date'),
      meta: { width: '120px' },
      cell: ({ row }) => (
        <span className="tabular text-[var(--fg-secondary)]">{date(row.original.date)}</span>
      ),
    })

    const planStart = timeColumn('planStart', t('attendance.planStart'), (row) => row.planStartAt)
    const planEnd = timeColumn('planEnd', t('attendance.planEnd'), (row) => row.planEndAt)
    const firstIn = timeColumn('firstIn', t('attendance.firstIn'), (row) => row.firstInAt)
    const lastOut = timeColumn('lastOut', t('attendance.lastOut'), (row) => row.lastOutAt)

    const late = exceptionHelper.display({
      id: 'lateMinutes',
      header: t('attendance.lateMinutes'),
      meta: { width: '132px', align: 'end' },
      cell: ({ row }) => <MinutesCell minutes={row.original.lateMinutes} emphasis />,
    })

    const early = exceptionHelper.display({
      id: 'earlyLeaveMinutes',
      header: t('attendance.earlyLeaveMinutes'),
      meta: { width: '132px', align: 'end' },
      cell: ({ row }) => <MinutesCell minutes={row.original.earlyLeaveMinutes} emphasis />,
    })

    const worked = exceptionHelper.display({
      id: 'worked',
      header: t('attendance.worked'),
      meta: { width: '112px', align: 'end', hideOnMobile: true },
      cell: ({ row }) => (
        <span className="tabular text-[var(--fg-secondary)]">
          {formatDuration(row.original.workedMinutes)}
        </span>
      ),
    })

    const status = exceptionHelper.display({
      id: 'status',
      header: t('employee.status'),
      meta: { width: '180px' },
      cell: ({ row }) => <DayStatusBadge status={row.original.status} />,
    })

    switch (kind) {
      case 'Late':
        return [employee, day, planStart, firstIn, late, worked, status]
      case 'EarlyLeave':
        return [employee, day, planEnd, lastOut, early, worked, status]
      case 'Absent':
        return [employee, day, planStart, planEnd, status]
      case 'Missing':
        return [employee, day, planStart, planEnd, firstIn, lastOut, worked, status]
    }
  }, [kind, t, date])
}

/** Vaqt ustuni — TOSHKENT vaqtida, sanasiz (sana alohida ustunda). */
function timeColumn(
  id: string,
  header: string,
  read: (row: AttendanceExceptionRow) => string | null,
): DataColumn<AttendanceExceptionRow> {
  return exceptionHelper.display({
    id,
    header,
    meta: { width: '112px' },
    cell: ({ row }) => {
      const value = read(row.original)
      return value === null ? (
        <span className="text-[var(--fg-tertiary)]">—</span>
      ) : (
        <span className="tabular">{formatPassTime(value)}</span>
      )
    },
  })
}

/**
 * Xulosa ustunlari.
 *
 * Daqiqalar «soat:daqiqa» ko'rinishida — HR «7 soat 59 daqiqa» deb o'qiydi
 * (`formatDuration`). Excel'da esa ular IKKI ustun: son va matn (SPEC §6.7).
 */
function useSummaryColumns(): Array<DataColumn<AttendanceSummaryRow>> {
  const { t, num } = useLocale()

  return useMemo(() => {
    const count = (
      id: keyof AttendanceSummaryRow,
      header: string,
      tone?: 'danger' | 'warning',
    ) =>
      summaryHelper.display({
        id,
        header,
        meta: { width: '112px', align: 'end' },
        cell: ({ row }) => {
          const value = row.original[id]
          const amount = typeof value === 'number' ? value : 0
          return (
            <span
              className={cn(
                'tabular',
                amount === 0 && 'text-[var(--fg-tertiary)]',
                amount > 0 && tone === 'danger' && 'font-semibold text-ios-red',
                amount > 0 && tone === 'warning' && 'font-semibold text-ios-orange',
              )}
            >
              {num(amount)}
            </span>
          )
        },
      })

    const duration = (id: keyof AttendanceSummaryRow, header: string, strong = false) =>
      summaryHelper.display({
        id,
        header,
        meta: { width: '112px', align: 'end' },
        cell: ({ row }) => {
          const value = row.original[id]
          return (
            <span className={cn('tabular', strong ? 'font-semibold' : 'text-[var(--fg-secondary)]')}>
              {formatDuration(typeof value === 'number' ? value : 0)}
            </span>
          )
        },
      })

    return [
      summaryHelper.display({
        id: 'employee',
        header: t('attendance.employee'),
        meta: { minWidth: '280px' },
        cell: ({ row }) => (
          <EmployeeCell
            employeeId={row.original.employeeId}
            name={row.original.employeeName}
            photoKey={row.original.photoKey}
            detail={[row.original.department, row.original.position].filter(Boolean).join(' · ')}
          />
        ),
      }),
      count('plannedDays', t('attendance.plannedDays')),
      count('presentDays', t('attendance.presentDays')),
      count('absentDays', t('attendance.absentDays'), 'danger'),
      count('dayOffWorkedDays', t('attendance.dayOffWorkedDays')),
      duration('workedMinutes', t('attendance.totalWorked'), true),
      duration('plannedMinutes', t('attendance.totalPlanned')),
      duration('lateMinutes', t('attendance.lateMinutes')),
      duration('earlyLeaveMinutes', t('attendance.earlyLeaveMinutes')),
      duration('overtimeMinutes', t('attendance.overtimeMinutes')),
      count('problemDays', t('attendance.problemDays'), 'warning'),
    ]
  }, [t, num])
}

/* ═════════════════════════════════════════════════════════ kataklar */

/**
 * Xodim — HAQIQIY RASM bilan (bosh harflar faqat rasm yo'q bo'lsa) va
 * ismi kartaga havola: hisobotdan «bu kim edi» degan savolga bir bosishda
 * o'tiladi.
 */
function EmployeeCell({
  employeeId,
  name,
  photoKey,
  detail,
}: {
  employeeId: string
  name: string
  photoKey: string | null
  detail: string
}) {
  return (
    <div className="flex min-w-0 items-center gap-3">
      <EmployeePhoto employeeId={employeeId} photoKey={photoKey} name={name} size="lg" />
      <div className="min-w-0">
        <Link
          to="/xodimlar/$employeeId"
          params={{ employeeId }}
          className="truncate-1 font-medium hover:text-[var(--accent-text)]"
        >
          {name}
        </Link>
        {detail !== '' && (
          <p className="truncate-1 text-caption1 text-[var(--fg-tertiary)]">{detail}</p>
        )}
      </div>
    </div>
  )
}

/** Daqiqa — «0:12» emas, «12 daq.»: kechikish qisqa va daqiqada o'ylanadi. */
function MinutesCell({ minutes, emphasis = false }: { minutes: number; emphasis?: boolean }) {
  const { t, num } = useLocale()

  if (minutes <= 0) return <span className="text-[var(--fg-tertiary)]">—</span>

  return (
    <span className={cn('tabular', emphasis && 'font-semibold text-ios-orange')}>
      {t('attendance.minutes', { n: num(minutes) })}
    </span>
  )
}

/* ═════════════════════════════════════════════════════════ yordamchilar */

function emptyToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}

/**
 * «Eng kam daqiqa» maydoni → son. Bo'sh, manfiy yoki butun bo'lmagan
 * qiymat — filtr yo'q (server ham 0 va manfiyni «filtrsiz» deb oladi).
 */
function parseMinutes(value: string): number | undefined {
  if (value.trim() === '') return undefined
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined
}
