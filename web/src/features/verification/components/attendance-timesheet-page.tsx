import { getRouteApi } from '@tanstack/react-router'
import {
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  CloudOff,
  FileSpreadsheet,
  ListFilter,
  LoaderCircle,
  RefreshCw,
  Search,
  SearchX,
  X,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { isRateLimited } from '@/features/employees/lib/employee-error'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { toSearchParam, useSearchDraft } from '@/shared/lib/list-search'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Button,
  Card,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Pagination,
  Select,
} from '@/shared/ui'

import type { TimesheetFilters, TimesheetRow } from '../api/attendance-api'
import { useExportTimesheet, useRecompute, useTimesheet } from '../api/attendance-queries'
import {
  ATTENDANCE_MANAGE,
  ATTENDANCE_VIEW,
  DEFAULT_PAGE_SIZE,
  REPORTS_EXPORT,
  monthLabel,
} from '../lib/attendance-presentation'
import {
  currentMonth,
  monthRange,
  shiftMonth,
  timesheetMonth,
  type MonthValue,
  type TimesheetSearch,
} from '../lib/attendance-search'
import { AttendanceTimesheetGrid, TimesheetLegend } from './attendance-timesheet-grid'

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`incidents-page.tsx` dagi izoh).
 */
const route = getRouteApi('/_app/verifikatsiya/tabel')

const EMPTY_ROWS: TimesheetRow[] = []
const EMPTY_DAYS: string[] = []

interface OrgFilters {
  legalEntityId: string
  departmentId: string
  positionId: string
}

const NO_ORG_FILTERS: OrgFilters = { legalEntityId: '', departmentId: '', positionId: '' }

/**
 * Tabel — xodim × kun, bir oy (SPEC §8.4, §8.6).
 *
 * Holat qayerda:
 *   · oy, sahifa va qidiruv — MANZILDA (`validateTimesheetSearch`): oy
 *     havola bilan birga yuradi, «orqaga» oldingi oyga qaytaradi;
 *   · firma, bo'lim va lavozim — sahifaning o'zida (`hodisalar.tsx`
 *     dagi qaror): ular kamdan-kam o'zgaradi.
 *
 * ⚠️ Kun HECH QACHON qo'lda tahrirlanmaydi (SPEC §1.2 #5) — ekranda
 * tahrirlash tugmasi yo'q va sahifa pastida TO'RT qonuniy tuzatish
 * yozilgan (§5.8). «Soatim bir soatga kamaydi» degan savolning birinchi
 * javobi esa katak oynachasidagi tanaffus satrida.
 */
export function AttendanceTimesheetPage() {
  const { t, num, locale, date, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canView = hasPermission(ATTENDANCE_VIEW)
  const canManage = hasPermission(ATTENDANCE_MANAGE)
  const canExport = hasPermission(REPORTS_EXPORT)
  // Xodim kartasi `employees.view` bilan himoyalangan — ruxsatsiz ism havola emas
  const canOpenEmployee = hasPermission(EMPLOYEES_VIEW)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const month = timesheetMonth(search)
  const page = search.page ?? 1
  const q = search.q ?? ''

  const [org, setOrg] = useState<OrgFilters>(NO_ORG_FILTERS)
  const [panelOpen, setPanelOpen] = useState(false)

  /**
   * Manzilni yangilash.
   *
   * Qidiruv va filtr — `replace` (bitta ekran ichidagi aniqlashtirish);
   * oy va sahifa — `push`: ular haqiqiy qadam va «orqaga» ularni qaytarishi
   * kerak (`employees-page.tsx` dagi qoida).
   */
  const setSearch = useCallback(
    (
      next: (previous: TimesheetSearch) => TimesheetSearch,
      history: 'push' | 'replace' = 'replace',
    ) => {
      void navigate({ search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  const [draft, setDraft] = useSearchDraft(q, (value) => {
    setSearch((previous) => ({ ...previous, q: toSearchParam(value), page: undefined }))
  })

  const options = useFilterOptions()

  /**
   * So'rov filtri — ekrandagi grid ham, Excel ham AYNAN shundan
   * («ekranda nima ko'rsang, faylda ham shu»).
   */
  const filters = useMemo<TimesheetFilters>(
    () => ({
      year: month.year,
      month: month.month,
      legalEntityId: org.legalEntityId === '' ? undefined : org.legalEntityId,
      departmentId: org.departmentId === '' ? undefined : org.departmentId,
      positionId: org.positionId === '' ? undefined : org.positionId,
      search: q.trim() === '' ? undefined : q,
    }),
    [month.year, month.month, org, q],
  )

  const query = useTimesheet({ ...filters, page, pageSize: DEFAULT_PAGE_SIZE }, canView)
  const exportFile = useExportTimesheet()
  const recompute = useRecompute()

  const data = query.data

  /*
    Oxirgi sahifadan narida qolib ketmaslik (`employees-page.tsx` dagi
    izoh): xodim bo'shatilgach ro'yxat qisqaradi yoki eski havolada
    `?page=7` turadi. `isPlaceholderData` da eski javobning `pageCount` iga
    qarab sakralmaydi.
  */
  useEffect(() => {
    if (data === undefined || query.isPlaceholderData) return

    const lastPage = Math.max(1, data.pageCount)
    if (page <= lastPage) return

    setSearch((previous) => ({ ...previous, page: lastPage <= 1 ? undefined : lastPage }))
  }, [data, query.isPlaceholderData, page, setSearch])

  const changeMonth = (next: MonthValue) => {
    // Oy — yangi qadam: tarixga yoziladi, sahifa esa boshiga qaytadi
    setSearch((previous) => ({ ...previous, year: next.year, month: next.month, page: undefined }), 'push')
    exportFile.reset()
    recompute.reset()
  }

  const changeOrg = (next: OrgFilters) => {
    setOrg(next)
    setSearch((previous) => ({ ...previous, page: undefined }))
  }

  const title = (
    <span className="flex items-center gap-3">
      <IconTile icon={CalendarDays} tone="brand" size="lg" />
      {t('attendance.timesheetTitle')}
    </span>
  )

  if (!canView) {
    return (
      <div className="space-y-6">
        <PageHeader title={title} />
        <Card>
          <EmptyState
            icon={CalendarDays}
            title={t('attendance.noAccessTitle')}
            description={t('attendance.noAccessText')}
          />
        </Card>
      </div>
    )
  }

  const rows = data?.items ?? EMPTY_ROWS
  const days = data?.days ?? EMPTY_DAYS
  const total = data?.total ?? 0
  const firstLoad = query.isPending

  const filtered =
    q.trim() !== '' || org.legalEntityId !== '' || org.departmentId !== '' || org.positionId !== ''
  const panelCount = [org.legalEntityId, org.departmentId, org.positionId].filter(
    (value) => value !== '',
  ).length

  const now = currentMonth()
  const isCurrentMonth = now.year === month.year && now.month === month.month

  /*
    Qayta hisoblash oralig'i — tanlangan oy, lekin BUGUNDAN narida emas.
    Kelajakdagi kunni hisoblash hali kelmagan ish kunini «kelmadi» deb
    yozardi. Butun oy kelajakda bo'lsa tugma o'chiriladi.
  */
  const range = monthRange(month)
  const today = todayIso()
  const recomputeTo = range.to > today ? today : range.to
  const recomputable = range.from <= today

  return (
    <div className="space-y-6">
      <PageHeader
        title={title}
        /*
          «Oxirgi hisoblash: HH:mm» (SPEC §8.7) — «tabel eskirganga
          o'xshaydi» degan shubhaga bir qarashda javob. Sana ham yoziladi:
          o'tgan oyning tabeli ko'rilayotgan bo'lishi mumkin.
        */
        subtitle={
          firstLoad
            ? t('common.loading')
            : data?.lastComputedAt
              ? t('attendance.lastComputedAt', { time: dateTime(data.lastComputedAt) })
              : t('attendance.notComputedYet')
        }
        actions={
          <>
            {canManage && (
              <Button
                type="button"
                disabled={!recomputable || recompute.isPending}
                title={
                  recomputable
                    ? t('attendance.recomputeRange', {
                        from: date(range.from),
                        to: date(recomputeTo),
                      })
                    : undefined
                }
                onClick={() =>
                  recompute.mutate({ from: range.from, to: recomputeTo, employeeId: null })
                }
              >
                {recompute.isPending ? <LoaderCircle className="animate-spin" /> : <RefreshCw />}
                <span className="hidden sm:inline">{t('attendance.recompute')}</span>
              </Button>
            )}
            {canExport && (
              <Button
                type="button"
                // Ikki marta bosilsa ikki fayl va tezlik cheklovi bekorga yeyiladi
                disabled={exportFile.isPending}
                onClick={() => exportFile.mutate(filters)}
              >
                {exportFile.isPending ? (
                  <LoaderCircle className="animate-spin" />
                ) : (
                  <FileSpreadsheet />
                )}
                <span className="hidden sm:inline">
                  {exportFile.isPending ? t('employees.exporting') : t('attendance.export')}
                </span>
              </Button>
            )}
          </>
        }
      />

      {/*
        Belgilar — sarlavha OSTIDA, DOIM (yuklanishda ham, bo'sh holatda ham —
        SPEC §8.6). Ilgari kartaning eng pastida edi va 50 qatordan keyin
        ko'rinmasdi (`TimesheetLegend` izohi).
      */}
      <TimesheetLegend />

      {query.isError && data !== undefined && (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {errorMessage(problemCode(query.error))}
        </Alert>
      )}

      {exportFile.isError && (
        <Alert tone="danger" title={t('employees.exportFailed')}>
          {isRateLimited(exportFile.error)
            ? t('employees.exportTooOften')
            : errorMessage(problemCode(exportFile.error))}
        </Alert>
      )}

      {/*
        «Boshlandi», «tayyor» EMAS (SPEC §6.7 — `202 Accepted`): aks holda HR
        darhol tabelga qarab, o'zgarmagan raqamni ko'radi va buni nosozlik
        deb o'ylaydi.
      */}
      {recompute.isSuccess && (
        <Alert tone="success">
          {t('attendance.recomputeStarted', {
            employees: num(recompute.data.employeeCount),
            days: num(recompute.data.dayCount),
          })}
        </Alert>
      )}
      {recompute.isError && (
        <Alert tone="danger">{errorMessage(problemCode(recompute.error))}</Alert>
      )}

      <Card className="overflow-hidden">
        <div className="border-b border-[var(--border)]">
          <div className="flex flex-wrap items-center gap-2.5 px-4 py-3.5 sm:gap-3 sm:px-5">
            {/*
              Oy tanlagich — strelkalar va «joriy oy». `<input type="month">`
              ishlatilmadi: Safari'ning ish stoli varianti uni oddiy matn
              maydoniga aylantiradi.
            */}
            <div className="flex items-center gap-1" role="group" aria-label={t('attendance.month')}>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={() => changeMonth(shiftMonth(month, -1))}
                aria-label={t('attendance.prevMonth')}
                title={t('attendance.prevMonth')}
              >
                <ChevronLeft />
              </Button>
              <span className="tabular min-w-[84px] text-center text-callout font-semibold">
                {monthLabel(month, locale)}
              </span>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={() => changeMonth(shiftMonth(month, 1))}
                aria-label={t('attendance.nextMonth')}
                title={t('attendance.nextMonth')}
              >
                <ChevronRight />
              </Button>
              {!isCurrentMonth && (
                <Button type="button" size="sm" variant="soft" pill onClick={() => changeMonth(now)}>
                  {t('attendance.currentMonth')}
                </Button>
              )}
            </div>

            <div className="min-w-[200px] flex-1">
              <Input
                icon={Search}
                shape="pill"
                type="search"
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                placeholder={t('common.searchPlaceholder')}
                aria-label={t('common.search')}
              />
            </div>

            <Button
              type="button"
              pill
              variant={panelOpen ? 'soft' : 'secondary'}
              aria-expanded={panelOpen}
              onClick={() => setPanelOpen((previous) => !previous)}
            >
              <ListFilter />
              <span className="hidden sm:inline">{t('common.filters')}</span>
              {panelCount > 0 && (
                <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
                  {num(panelCount)}
                </span>
              )}
            </Button>

            {/*
              Tozalash — «Filtrlar» yonida, faqat filtr qo'yilganda. Panel ostidagi
              alohida qator ro'yxatdan joy olardi (egasi 2026-10-22).
            */}
            {filtered && (
              <Button
                type="button"
                pill
                variant="ghost"
                title={t('employees.clearFilters')}
                aria-label={t('employees.clearFilters')}
                onClick={() => {
                  changeOrg(NO_ORG_FILTERS)
                  setDraft('')
                }}
              >
                <X />
                <span className="hidden xl:inline">{t('employees.clearFilters')}</span>
              </Button>
            )}
          </div>

          {panelOpen && (
            <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                <Select
                  label={t('employee.legalEntity')}
                  placeholder={t('employees.anyLegalEntity')}
                  options={options.legalEntities}
                  value={org.legalEntityId}
                  onChange={(event) => changeOrg({ ...org, legalEntityId: event.target.value })}
                />
                <Select
                  label={t('employee.department')}
                  placeholder={t('employees.anyDepartment')}
                  options={options.departments}
                  value={org.departmentId}
                  onChange={(event) => changeOrg({ ...org, departmentId: event.target.value })}
                />
                <Select
                  label={t('employee.position')}
                  placeholder={t('employees.anyPosition')}
                  options={options.positions}
                  value={org.positionId}
                  onChange={(event) => changeOrg({ ...org, positionId: event.target.value })}
                />
              </div>
            </div>
          )}
        </div>

        {query.isError && data === undefined ? (
          /*
            Birinchi yuklash yiqildi — bo'sh jadval qobig'i emas, sabab va
            qayta urinish. Keyingi yuklashlardagi xato esa eski gridni
            yashirmaydi: tepadagi ogohlantirish yetarli.
          */
          <EmptyState
            icon={CloudOff}
            title={t('employees.loadFailed')}
            description={errorMessage(problemCode(query.error))}
            action={
              <Button type="button" onClick={() => void query.refetch()} disabled={query.isFetching}>
                {t('common.retry')}
              </Button>
            }
          />
        ) : !firstLoad && rows.length === 0 ? (
          filtered ? (
            <EmptyState
              icon={SearchX}
              title={t('common.empty')}
              description={t('attendance.notFoundText')}
            />
          ) : (
            <EmptyState
              icon={CalendarDays}
              title={t('attendance.timesheetEmptyTitle')}
              description={t('attendance.timesheetEmptyText')}
            />
          )
        ) : (
          <AttendanceTimesheetGrid
            days={days}
            rows={rows}
            loading={firstLoad}
            canOpenEmployee={canOpenEmployee}
          />
        )}

        {total > 0 && (
          <Pagination
            page={page}
            pageSize={DEFAULT_PAGE_SIZE}
            total={total}
            onPageChange={(next) =>
              setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push')
            }
            disabled={query.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>

      {/*
        To'rt qonuniy tuzatish (SPEC §5.8). Doimiy va sokin: bu ogohlantirish
        emas, «tabelni qanday tuzatiladi» degan savolga oldindan berilgan
        javob — HR uni qo'llab-quvvatlashga yozishdan OLDIN o'qishi kerak.
      */}
      <Alert tone="info" title={t('attendance.cannotEditTitle')}>
        {t('attendance.cannotEditText')}
      </Alert>
    </div>
  )
}

