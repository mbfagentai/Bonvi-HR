import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { getRouteApi, Link } from '@tanstack/react-router'
import {
  ArrowDownAZ,
  ArrowUpZA,
  ChartBar,
  ChartPie,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ListFilter,
  Menu,
  Pin,
  RefreshCw,
  Search,
  SearchX,
  Users,
  X,
  type LucideIcon,
} from 'lucide-react'
import { useCallback, useEffect, useId, useMemo, useRef, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { toSearchParam, useSearchDraft } from '@/shared/lib/list-search'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  Input,
  Select,
  Skeleton,
  dataColumnHelper,
  type AvatarSize,
  type DataColumn,
  type SelectOption,
} from '@/shared/ui'

import type {
  DashboardItem,
  DashboardFilters,
  DashboardSort,
  DashboardStatus,
} from '../api/attendance-api'
import { useAttendanceDashboard, useFormerEmployeeAlerts } from '../api/attendance-queries'
import {
  DASHBOARD_COLUMNS,
  useDashboardPrefs,
  type DashboardColumn,
} from '../lib/attendance-dashboard-prefs'
import {
  ATTENDANCE_VIEW,
  DASHBOARD_STATUSES,
  DASHBOARD_STATUS_LOOK,
  dashboardPercent,
  formatPassTime,
} from '../lib/attendance-presentation'
import {
  DASHBOARD_PAGE_SIZES,
  DEFAULT_DASHBOARD_PAGE_SIZE,
  dashboardPinOf,
  hasPinValues,
  readIsoDate,
  samePin,
  shiftIsoDate,
  type DashboardPageSize,
  type DashboardSearch,
} from '../lib/attendance-search'
import {
  DashboardChart,
  DashboardChartSkeleton,
  DashboardLegend,
  type DashboardChartType,
  type DashboardSlice,
} from './attendance-dashboard-chart'
import { FormerEmployeeAlertPanel } from './attendance-former-employee-alert'
import { AttendanceLoadError, AttendanceNoAccess } from './attendance-manage-states'

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`incidents-page.tsx` dagi izoh). Yo'l ATAYLAB eskicha (`/jonli`):
 * menyu, qurilmalar sahifasidagi qizil qator va saqlangan xatcho'plar shu
 * manzilga olib keladi.
 */
const route = getRouteApi('/_app/verifikatsiya/jonli')

const helper = dataColumnHelper<DashboardItem>()

/** Modul darajasida — har renderda yangi massiv jadval modelini qayta qurardi. */
const EMPTY_ITEMS: DashboardItem[] = []

/** Ochiluvchi menyu bandi — `language-switcher.tsx` va `user-menu.tsx` bilan bir xil shakl. */
const MENU_ITEM = cn(
  'flex h-9 cursor-pointer items-center gap-2.5 rounded-md px-2.5 text-subhead outline-none select-none',
  'transition-colors duration-100 data-[highlighted]:bg-[var(--bg-hover)]',
  'data-[disabled]:cursor-default data-[disabled]:text-[var(--fg-tertiary)]',
)

const MENU_PANEL =
  'pop-panel z-50 rounded-xl border border-[var(--border)] bg-[var(--bg-elevated)] p-1.5 shadow-floating outline-none'

const MENU_LABEL =
  'px-2.5 pt-1.5 pb-2 text-caption2 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase'

/** Ustun sarlavhalari — ustunlar menyusi va jadval bitta xaritadan o'qiydi. */
const COLUMN_LABEL_KEY: Readonly<Record<DashboardColumn, string>> = {
  position: 'attendance.statsColPosition',
  arrived: 'attendance.statsColArrived',
  left: 'attendance.statsColLeft',
  status: 'attendance.statsColStatus',
}

/**
 * Davomat statistikasi — Verifix'dagi «Статистика посещений сотрудников»
 * ekranining o'rnini bosadi (egasining talabi, 2026-09-17: «asosiy sahifa
 * rasmdagidek bo'lsin»).
 *
 * Bitta keng karta:
 *   · sarlavha qatori — nom, kun tanlagich va diagramma turi;
 *   · chapda — diagramma (markazda jami) va legenda;
 *   · o'ngda — asboblar qatori va xodimlar jadvali.
 * `lg` dan tor ekranda diagramma kartasi jadval ustiga tushadi va jadval
 * kartalarga aylanadi.
 *
 * Holat qayerda:
 *   · kun, qidiruv, holat, firma/bo'lim/lavozim, saralash, sahifa va uning
 *     hajmi — MANZILDA (`validateDashboardSearch`);
 *   · qadalgan filtrlar va ustunlar — brauzerda, foydalanuvchi bo'yicha
 *     (`attendance-dashboard-prefs.ts`);
 *   · diagramma turi — sahifaning o'zida: bu bir martalik ko'rinish tanlovi.
 *
 * ⭐ Tanlangan kun BUGUN bo'lsa sahifa 30 soniyada bir yangilanadi va hech
 * qachon BO'SHAMAYDI (`useAttendanceDashboard` izohi). O'tgan kun so'ralmaydi.
 */
export function AttendanceDashboardPage() {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission, user } = useAuth()

  const canView = hasPermission(ATTENDANCE_VIEW)
  /*
    Xodim kartasi `employees.view` bilan himoyalangan (server). Ruxsat
    bo'lmasa ism HAVOLA EMAS — oddiy matn: bosiladigan, lekin 403 ga olib
    boradigan havola bajarilmaydigan va'da bo'lardi.
  */
  const canOpenEmployee = hasPermission(EMPLOYEES_VIEW)

  const titleId = useId()
  const search = route.useSearch()
  const navigate = route.useNavigate()

  /*
    «Bugun» har renderda qayta olinadi: sahifa yarim tundan keyin ham ochiq
    turadi va keyingi 30 soniyalik yangilanish o'zi yangi kunga o'tadi
    (manzilda sana yo'q — `DashboardSearch.date` izohi).
  */
  const today = todayIso()
  const selectedDate = search.date ?? today
  const isToday = selectedDate === today
  const q = search.q ?? ''
  const status = search.status ?? null
  const sort: DashboardSort = search.sort ?? 'name'
  const pageSize: DashboardPageSize = search.size ?? DEFAULT_DASHBOARD_PAGE_SIZE
  const page = search.page ?? 1

  /**
   * Manzilni yangilash. Qidiruv, filtr va saralash — `replace` (bitta
   * ekran ichidagi aniqlashtirish); kun va sahifa — `push`: ular haqiqiy
   * qadam va «orqaga» ularni qaytarishi kerak (`employees-page.tsx` qoidasi).
   */
  const setSearch = useCallback(
    (
      next: (previous: DashboardSearch) => DashboardSearch,
      history: 'push' | 'replace' = 'replace',
    ) => {
      void navigate({ search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  const [draft, setDraft] = useSearchDraft(q, (value) => {
    setSearch((previous) => ({ ...previous, q: toSearchParam(value), page: undefined }))
  })

  const [chartType, setChartType] = useState<DashboardChartType>('donut')

  const prefs = useDashboardPrefs(user?.id ?? 'anonymous')
  const currentPin = dashboardPinOf(search)
  const pinned = prefs.pin !== null && samePin(prefs.pin, currentPin)
  const hidden = useMemo(() => new Set<DashboardColumn>(prefs.hidden), [prefs.hidden])

  /*
    Qadalgan filtrlar sahifaga KELGANDA bir marta qo'llanadi — faqat manzilda
    o'sha maydonlardan birortasi ham bo'lmasa. Havola bilan kelgan aniq
    filtr (`?department=…`) ustidan yozilmaydi, filtrni o'zi tozalagan HR
    esa uni qayta qo'llanib qolganini ko'rmaydi (effekt qayta ishlamaydi).
  */
  const pinChecked = useRef(false)
  useEffect(() => {
    if (pinChecked.current) return
    pinChecked.current = true

    const pin = prefs.pin
    if (pin === null || hasPinValues(dashboardPinOf(search))) return

    setSearch((previous) => ({ ...previous, ...pin, page: undefined }))
    // Faqat birinchi kirishda — keyingi o'zgarishlar foydalanuvchining o'z qarori
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const options = useFilterOptions()

  const filters = useMemo<DashboardFilters>(
    () => ({
      // Sana berilmasa server bugunni oladi — yarim tundan keyin kalit ham,
      // javob ham o'zi yangi kunga o'tadi
      date: search.date,
      search: q.trim() === '' ? undefined : q,
      legalEntityId: search.entity,
      departmentId: search.department,
      positionId: search.position,
      status: search.status,
      sort: search.sort,
      page,
      pageSize,
    }),
    [
      search.date,
      q,
      search.entity,
      search.department,
      search.position,
      search.status,
      search.sort,
      page,
      pageSize,
    ],
  )

  const query = useAttendanceDashboard(filters, isToday, canView)

  /*
    Ogohlantirish statistikaning O'Z javobidan emas, alohida so'rovdan
    (`useFormerEmployeeAlertsQuery` izohi): menyudagi qizil nishon bilan
    bitta kesh yozuvi, va statistikaning og'ir hisobi yiqilganda ham
    xavfsizlik hodisasi ekrandan yo'qolmaydi.
  */
  const alerts = useFormerEmployeeAlerts(canView)

  const data = query.data

  /*
    Oxirgi sahifadan narida qolib ketmaslik (`employees-page.tsx` dagi
    izoh): filtr torayganda yoki eski havolada `?page=7` turganda.
    `isPlaceholderData` da eski javobning `pageCount` iga qarab sakralmaydi.
  */
  useEffect(() => {
    if (data === undefined || query.isPlaceholderData) return

    const lastPage = Math.max(1, data.pageCount)
    if (page <= lastPage) return

    setSearch((previous) => ({ ...previous, page: lastPage <= 1 ? undefined : lastPage }))
  }, [data, query.isPlaceholderData, page, setSearch])

  const columns = useMemo<Array<DataColumn<DashboardItem>>>(() => {
    const list: Array<DataColumn<DashboardItem>> = [
      helper.accessor('fullName', {
        header: t('attendance.statsColName'),
        meta: { minWidth: '200px' },
        cell: ({ row }) => <EmployeeName item={row.original} linked={canOpenEmployee} />,
      }),
    ]

    if (!hidden.has('position')) {
      list.push(
        helper.accessor('positionName', {
          header: t(COLUMN_LABEL_KEY.position),
          enableSorting: false,
          meta: { minWidth: '140px' },
          cell: ({ row }) => (
            <span className="truncate-2 text-[var(--fg-secondary)]">
              {row.original.positionName ?? '—'}
            </span>
          ),
        }),
      )
    }

    if (!hidden.has('arrived')) {
      list.push(
        helper.accessor('arrivedAt', {
          header: t(COLUMN_LABEL_KEY.arrived),
          enableSorting: false,
          meta: { width: '92px' },
          cell: ({ row }) => <TimeCell value={row.original.arrivedAt} />,
        }),
      )
    }

    if (!hidden.has('left')) {
      list.push(
        helper.accessor('leftAt', {
          header: t(COLUMN_LABEL_KEY.left),
          enableSorting: false,
          meta: { width: '92px' },
          cell: ({ row }) => <TimeCell value={row.original.leftAt} />,
        }),
      )
    }

    if (!hidden.has('status')) {
      list.push(
        helper.accessor('status', {
          header: t(COLUMN_LABEL_KEY.status),
          enableSorting: false,
          meta: { width: '180px' },
          cell: ({ row }) => <StatusText item={row.original} />,
        }),
      )
    }

    return list
  }, [t, canOpenEmployee, hidden])

  /** Jadval saralashi SERVERDA — faqat F.I.Sh. bo'yicha (shartnoma). */
  const sorting = useMemo(() => [{ id: 'fullName', desc: sort === '-name' }], [sort])

  if (!canView) {
    return <AttendanceNoAccess icon={ChartPie} title={t('attendance.statsTitle')} />
  }

  /* ───────────────────────────────────────────────── amallar */

  const changeDate = (next: string) => {
    if (readIsoDate(next) === undefined) return

    // Kelajak kun hali davomat emas — tanlagich bugundan nariga o'tkazmaydi
    const clamped = next > today ? today : next
    setSearch(
      (previous) => ({
        ...previous,
        date: clamped === today ? undefined : clamped,
        page: undefined,
      }),
      'push',
    )
  }

  /** Bo'lak, ustun yoki legenda: birinchi bosish — filtr, ikkinchisi — olib tashlash. */
  const toggleStatus = (next: DashboardStatus) => {
    setSearch((previous) => ({
      ...previous,
      status: previous.status === next ? undefined : next,
      page: undefined,
    }))
  }

  const changeOrg = (patch: Pick<DashboardSearch, 'entity' | 'department' | 'position'>) => {
    setSearch((previous) => ({ ...previous, ...patch, page: undefined }))
  }

  const changeSort = (next: DashboardSort) => {
    setSearch((previous) => ({
      ...previous,
      sort: next === 'name' ? undefined : next,
      page: undefined,
    }))
  }

  const changePageSize = (next: DashboardPageSize) => {
    setSearch((previous) => ({
      ...previous,
      size: next === DEFAULT_DASHBOARD_PAGE_SIZE ? undefined : next,
      page: undefined,
    }))
  }

  const changePage = (next: number) => {
    setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push')
  }

  const clearFilters = () => {
    setSearch((previous) => ({
      ...previous,
      q: undefined,
      status: undefined,
      entity: undefined,
      department: undefined,
      position: undefined,
      page: undefined,
    }))
  }

  const togglePin = () => {
    // Sukut holatini «eslab qolish» — qadalganini olib tashlash bilan bir xil
    prefs.savePin(pinned || !hasPinValues(currentPin) ? null : currentPin)
  }

  /* ───────────────────────────────────────────────── ko'rinish */

  const firstLoad = query.isPending
  const loadFailed = query.isError && data === undefined
  /** Kun, sahifa yoki filtr almashdi — eski surat turibdi, yangisi kelmoqda. */
  const switching = query.isPlaceholderData && query.isFetching

  const items = data?.items ?? EMPTY_ITEMS
  const total = data?.total ?? 0
  const totalItems = data?.totalItems ?? 0
  const pageCount = Math.max(1, data?.pageCount ?? 1)

  const slices: DashboardSlice[] = DASHBOARD_STATUSES.map((item) => {
    const count = data?.counts[DASHBOARD_STATUS_LOOK[item].count] ?? 0
    return { status: item, count, percent: dashboardPercent(count, total) }
  })

  const orgCount = [search.entity, search.department, search.position].filter(
    (value) => value !== undefined,
  ).length
  const filtered = q.trim() !== '' || status !== null || orgCount > 0
  const pinnable = pinned || prefs.pin !== null || hasPinValues(currentPin)

  const emptyState = filtered ? (
    <EmptyState
      icon={SearchX}
      title={t('common.empty')}
      description={t('attendance.notFoundText')}
      action={
        <Button type="button" size="sm" variant="ghost" onClick={clearFilters}>
          <X />
          {t('employees.clearFilters')}
        </Button>
      }
    />
  ) : (
    <EmptyState
      icon={Users}
      title={t('attendance.statsEmptyTitle')}
      description={t('attendance.statsEmptyText')}
    />
  )

  return (
    <div className="space-y-6">
      {/*
        ⚠️ ENG TEPADA, kartadan YUQORIDA (SPEC §8.4, egasining talabi):
        xavfsizlik hodisasi diagramma ostida «yana bitta raqam» bo'lib
        qolmasligi kerak. Faqat ogohlantirish BOR bo'lsa chiziladi.
      */}
      {alerts.length > 0 && <FormerEmployeeAlertPanel alerts={alerts} />}

      {/* Keyingi yangilanish yiqildi — eski surat joyida qoladi, sabab tepada */}
      {query.isError && data !== undefined && (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {errorMessage(problemCode(query.error))}
        </Alert>
      )}

      {/*
        Bitta keng karta `lg` dan boshlab; torroq ekranda — ikki karta ustma-ust.

        NEGA `lg:contents`: sarlavha va diagramma tor ekranda BITTA kartada
        turishi, kengda esa sarlavha ikkala ustun ustiga cho'zilishi kerak.
        `display: contents` kartaning o'z qutisini (chegara, fon, soya)
        olib tashlaydi va uning bolalarini to'g'ridan-to'g'ri tashqi
        gridga beradi — DOM bir xil qoladi, JS da o'lcham o'qilmaydi.

        `overflow-clip`, `hidden` emas: `hidden` o'rovchini aylantiriladigan
        konteynerga aylantiradi va diagramma ustunining `sticky` xususiyati
        ishlamay qolardi.
      */}
      <section
        aria-labelledby={titleId}
        className={cn(
          'space-y-4',
          'lg:grid lg:grid-cols-[300px_minmax(0,1fr)] lg:space-y-0 xl:grid-cols-[340px_minmax(0,1fr)] 2xl:grid-cols-[400px_minmax(0,1fr)]',
          'lg:overflow-clip lg:rounded-lg lg:border lg:border-[var(--border)] lg:bg-[var(--bg-elevated)] lg:shadow-card',
        )}
      >
        <Card className="lg:contents">
          <header className="flex flex-wrap items-center justify-between gap-x-4 gap-y-3 px-4 pt-4 sm:px-5 sm:pt-5 lg:col-span-2 lg:border-b lg:border-[var(--border)] lg:pb-4">
            <div className="flex min-w-0 items-center gap-3">
              <IconTile icon={ChartPie} tone="brand" size="lg" />
              <div className="min-w-0">
                <h1 id={titleId} className="text-title2 leading-tight font-semibold tracking-tight">
                  {t('attendance.statsTitle')}
                </h1>
                <p className="mt-1 text-footnote text-[var(--fg-secondary)]">
                  {data === undefined
                    ? t('common.loading')
                    : t('attendance.refreshedAt', {
                        // Server lahzasi — brauzer soati emas: eski javob
                        // «hozirgina yangilandi» deb yolg'on gapirmasin
                        time: formatPassTime(data.asOf),
                      })}
                </p>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <DateStepper value={selectedDate} today={today} onChange={changeDate} />
              <ChartTypePicker value={chartType} onChange={setChartType} />
            </div>
          </header>

          {loadFailed ? (
            /*
              Birinchi yuklash yiqildi — bo'sh diagramma («0 xodim») emas,
              sabab va qayta urinish: nol bu yerda yolg'on bo'lardi.
            */
            <AttendanceLoadError
              className="lg:col-span-2"
              error={query.error}
              onRetry={() => void query.refetch()}
              retrying={query.isFetching}
            />
          ) : (
            <div
              className={cn(
                'px-4 pt-2 pb-4 sm:px-5 sm:pb-5',
                // Uzun jadval aylantirilganda diagramma va legenda ko'z oldida
                // qoladi — legendani bosib filtrlash uchun tepaga qaytish shart emas
                'lg:sticky lg:top-0 lg:self-start lg:pt-5',
              )}
            >
              {firstLoad ? (
                <DashboardChartSkeleton />
              ) : (
                <>
                  <DashboardChart
                    type={chartType}
                    slices={slices}
                    total={total}
                    active={status}
                    onToggle={toggleStatus}
                  />
                  <div className="mt-3">
                    <DashboardLegend slices={slices} active={status} onToggle={toggleStatus} />
                  </div>
                </>
              )}
            </div>
          )}
        </Card>

        {!loadFailed && (
          <div className="min-w-0 space-y-3 lg:space-y-0 lg:border-l lg:border-[var(--border)]">
            <Card className="lg:rounded-none lg:border-0 lg:border-b lg:border-[var(--border)] lg:bg-transparent lg:shadow-none">
              <div className="flex flex-wrap items-center gap-2 p-3 sm:px-4">
                <div className="min-w-[180px] flex-1 basis-[220px]">
                  <Input
                    icon={Search}
                    shape="pill"
                    type="search"
                    value={draft}
                    onChange={(event) => setDraft(event.target.value)}
                    placeholder={t('attendance.statsSearch')}
                    aria-label={t('common.search')}
                  />
                </div>

                <div className="flex flex-wrap items-center gap-1">
                  {status !== null && (
                    <StatusChip status={status} onClear={() => toggleStatus(status)} />
                  )}

                  <FilterPopover
                    entity={search.entity}
                    department={search.department}
                    position={search.position}
                    legalEntities={options.legalEntities}
                    departments={options.departments}
                    positions={options.positions}
                    onChange={changeOrg}
                  />

                  <Button
                    type="button"
                    variant={pinned ? 'soft' : 'ghost'}
                    size="icon"
                    aria-pressed={pinned}
                    disabled={!pinnable}
                    onClick={togglePin}
                    title={pinned ? t('attendance.statsUnpin') : t('attendance.statsPin')}
                    aria-label={pinned ? t('attendance.statsUnpin') : t('attendance.statsPin')}
                  >
                    <Pin className={cn(pinned && 'fill-current')} />
                  </Button>

                  {/* Tor ekranda jadval sarlavhasi yo'q — saralash shu tugmada */}
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="lg:hidden"
                    onClick={() => changeSort(sort === 'name' ? '-name' : 'name')}
                    title={sort === 'name' ? t('attendance.statsSortAsc') : t('attendance.statsSortDesc')}
                    aria-label={sort === 'name' ? t('attendance.statsSortAsc') : t('attendance.statsSortDesc')}
                  >
                    {sort === 'name' ? <ArrowDownAZ /> : <ArrowUpZA />}
                  </Button>

                  <PageSizeMenu pageSize={pageSize} total={totalItems} onChange={changePageSize} />

                  <CompactPager
                    page={page}
                    pageCount={pageCount}
                    onChange={changePage}
                    disabled={switching}
                  />

                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    onClick={() => void query.refetch()}
                    title={t('attendance.statsRefresh')}
                    aria-label={t('attendance.statsRefresh')}
                  >
                    <RefreshCw className={cn(query.isFetching && 'animate-spin')} />
                  </Button>

                  <ColumnsMenu hidden={hidden} onChange={prefs.setColumnVisible} />
                </div>
              </div>
            </Card>

            <div
              className={cn(
                'transition-opacity duration-200 ease-[var(--ease-ios)]',
                switching && 'opacity-60',
              )}
              aria-busy={switching || firstLoad}
            >
              {/* ---------- lg+: jadval ---------- */}
              <div className="hidden lg:block">
                <DataTable
                  columns={columns}
                  data={items}
                  getRowId={(item) => item.employeeId}
                  loading={firstLoad}
                  skeletonRows={8}
                  sorting={sorting}
                  onSortingChange={(next) => changeSort(next[0]?.desc === true ? '-name' : 'name')}
                  onRowClick={
                    canOpenEmployee
                      ? (item) =>
                          void navigate({
                            to: '/xodimlar/$employeeId',
                            params: { employeeId: item.employeeId },
                          })
                      : undefined
                  }
                  rowLabel={(item) => item.fullName}
                  empty={emptyState}
                />
              </div>

              {/* ---------- lg dan tor: kartalar ---------- */}
              <div className="lg:hidden">
                {firstLoad ? (
                  <ul className="grid gap-3 sm:grid-cols-2">
                    {Array.from({ length: 4 }, (_, index) => (
                      <li key={`skeleton-${index}`}>
                        <Card className="flex items-center gap-3 p-4">
                          <Skeleton className="size-12 rounded-full" />
                          <span className="flex-1 space-y-2">
                            <Skeleton className="h-4 w-36" />
                            <Skeleton className="h-3 w-24" />
                          </span>
                        </Card>
                      </li>
                    ))}
                  </ul>
                ) : items.length === 0 ? (
                  <Card>{emptyState}</Card>
                ) : (
                  <ul className="grid gap-3 sm:grid-cols-2">
                    {items.map((item) => (
                      <li key={item.employeeId}>
                        <EmployeeCard item={item} linked={canOpenEmployee} hidden={hidden} />
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </div>
        )}
      </section>
    </div>
  )
}

/* ════════════════════════════════════════════════════════════ sarlavha */

/**
 * Kun tanlagich: ‹ sana › va «Bugun».
 *
 * `<input type="date">` — brauzerning o'z kalendari (macOS/iOS da tizim
 * g'ildiragi). Kelajak kun tanlanmaydi (`max`): u hali davomat emas.
 */
function DateStepper({
  value,
  today,
  onChange,
}: {
  value: string
  today: string
  onChange: (next: string) => void
}) {
  const { t } = useLocale()

  return (
    <div role="group" aria-label={t('attendance.date')} className="flex items-center gap-1">
      <Button
        type="button"
        variant="ghost"
        size="icon"
        onClick={() => onChange(shiftIsoDate(value, -1))}
        title={t('attendance.statsPrevDay')}
        aria-label={t('attendance.statsPrevDay')}
      >
        <ChevronLeft />
      </Button>

      <div className="w-[164px]">
        <Input
          type="date"
          shape="pill"
          className="tabular"
          value={value}
          max={today}
          required
          aria-label={t('attendance.date')}
          // Tozalash tugmasi bosilsa bo'sh satr keladi — kun «yo'q» bo'lolmaydi
          onChange={(event) => {
            if (event.target.value !== '') onChange(event.target.value)
          }}
        />
      </div>

      <Button
        type="button"
        variant="ghost"
        size="icon"
        disabled={value >= today}
        onClick={() => onChange(shiftIsoDate(value, 1))}
        title={t('attendance.statsNextDay')}
        aria-label={t('attendance.statsNextDay')}
      >
        <ChevronRight />
      </Button>

      {value !== today && (
        <Button type="button" size="sm" variant="soft" pill onClick={() => onChange(today)}>
          {t('attendance.statsToday')}
        </Button>
      )}
    </div>
  )
}

const CHART_TYPES: ReadonlyArray<{ value: DashboardChartType; labelKey: string; icon: LucideIcon }> = [
  { value: 'donut', labelKey: 'attendance.statsChartDonut', icon: ChartPie },
  { value: 'bars', labelKey: 'attendance.statsChartBars', icon: ChartBar },
]

/**
 * Diagramma turi — ochiluvchi ro'yxat va «✕» (doiraviyga qaytarish).
 *
 * «✕» faqat ustunli tur tanlanganda chiziladi: sukut holatida u hech
 * narsani o'zgartirmaydigan tugma bo'lib qolardi.
 */
function ChartTypePicker({
  value,
  onChange,
}: {
  value: DashboardChartType
  onChange: (next: DashboardChartType) => void
}) {
  const { t } = useLocale()
  const current = CHART_TYPES.find((item) => item.value === value) ?? CHART_TYPES[0]
  if (current === undefined) return null

  const CurrentIcon = current.icon

  return (
    <div className="flex items-center gap-1">
      <DropdownMenu.Root modal={false}>
        <DropdownMenu.Trigger asChild>
          <Button
            type="button"
            pill
            className="data-[state=open]:border-[var(--accent)]"
            aria-label={`${t('attendance.statsChartType')}: ${t(current.labelKey)}`}
          >
            <CurrentIcon />
            <span className="hidden sm:inline">{t(current.labelKey)}</span>
            <ChevronDown className="text-[var(--fg-tertiary)]" />
          </Button>
        </DropdownMenu.Trigger>

        <DropdownMenu.Portal>
          <DropdownMenu.Content align="end" sideOffset={8} className={cn(MENU_PANEL, 'min-w-[220px]')}>
            <DropdownMenu.Label className={MENU_LABEL}>{t('attendance.statsChartType')}</DropdownMenu.Label>
            <DropdownMenu.RadioGroup
              value={value}
              onValueChange={(next) => {
                const found = CHART_TYPES.find((item) => item.value === next)
                if (found !== undefined) onChange(found.value)
              }}
            >
              {CHART_TYPES.map((item) => {
                const Icon = item.icon
                return (
                  <DropdownMenu.RadioItem key={item.value} value={item.value} className={MENU_ITEM}>
                    <Icon className="size-4 shrink-0 text-[var(--fg-secondary)]" strokeWidth={2} />
                    <span className="flex-1">{t(item.labelKey)}</span>
                    <DropdownMenu.ItemIndicator>
                      <Check className="size-4 text-[var(--accent-on-soft)]" strokeWidth={3} />
                    </DropdownMenu.ItemIndicator>
                  </DropdownMenu.RadioItem>
                )
              })}
            </DropdownMenu.RadioGroup>
          </DropdownMenu.Content>
        </DropdownMenu.Portal>
      </DropdownMenu.Root>

      {value !== 'donut' && (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          onClick={() => onChange('donut')}
          title={t('attendance.statsChartReset')}
          aria-label={t('attendance.statsChartReset')}
        >
          <X />
        </Button>
      )}
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════ asboblar */

/** Tanlangan holat — qidiruv yonidagi olib tashlanadigan chip. */
function StatusChip({ status, onClear }: { status: DashboardStatus; onClear: () => void }) {
  const { t } = useLocale()
  const look = DASHBOARD_STATUS_LOOK[status]

  return (
    <span className="inline-flex h-8 items-center gap-1.5 rounded-full bg-[var(--bg-sunken)] pr-1 pl-3 text-footnote font-medium">
      <span aria-hidden className={cn('size-2 shrink-0 rounded-full', look.dotClass)} />
      <span className="max-w-[180px] truncate">{t(look.legendKey)}</span>
      <button
        type="button"
        onClick={onClear}
        title={t('attendance.statsClearStatus')}
        aria-label={t('attendance.statsClearStatus')}
        className={cn(
          'grid size-6 place-items-center rounded-full text-[var(--fg-tertiary)]',
          'transition-colors duration-[120ms] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
        )}
      >
        <X className="size-3.5" strokeWidth={2.5} />
      </button>
    </span>
  )
}

/**
 * Firma, bo'lim va lavozim — ochiluvchi panelda.
 *
 * `role="dialog"`: ichida menyu bandlari emas, forma maydonlari
 * (`attendance-timesheet-grid.tsx` dagi popover bilan bir xil sabab).
 *
 * ⚠️ Ichki `onKeyDown` dagi `stopPropagation` SHART: Radix menyusi Tab ni
 * va harfli tugmalarni o'zi ushlaydi (menyu bandlari orasida yurish uchun)
 * — busiz `<select>` ga klaviatura bilan o'tib ham, uni strelka bilan
 * almashtirib ham bo'lmasdi. Escape baribir yopadi: u hujjat darajasida
 * tinglanadi.
 */
function FilterPopover({
  entity,
  department,
  position,
  legalEntities,
  departments,
  positions,
  onChange,
}: {
  entity: string | undefined
  department: string | undefined
  position: string | undefined
  legalEntities: readonly SelectOption[]
  departments: readonly SelectOption[]
  positions: readonly SelectOption[]
  onChange: (patch: Pick<DashboardSearch, 'entity' | 'department' | 'position'>) => void
}) {
  const { t, num } = useLocale()
  const count = [entity, department, position].filter((value) => value !== undefined).length

  /*
    Ochilganda fokus BIRINCHI maydonga o'tadi. Radix menyusi fokusni o'z
    konteyneriga qo'yadi va u yerdan Tab ni o'zi ushlaydi — busiz panelni
    klaviatura bilan umuman ishlatib bo'lmasdi. Kadr kechiktirilgan: Radix
    o'z fokusini qo'yib bo'lgandan KEYIN ishlashi kerak.
  */
  const [open, setOpen] = useState(false)
  const firstField = useRef<HTMLSelectElement>(null)

  useEffect(() => {
    if (!open) return
    const frame = requestAnimationFrame(() => firstField.current?.focus())
    return () => cancelAnimationFrame(frame)
  }, [open])

  return (
    <DropdownMenu.Root modal={false} open={open} onOpenChange={setOpen}>
      <DropdownMenu.Trigger asChild>
        <Button
          type="button"
          variant={count > 0 ? 'soft' : 'ghost'}
          size="icon"
          className="relative"
          title={t('common.filters')}
          aria-label={t('common.filters')}
        >
          <ListFilter />
          {count > 0 && (
            <span className="tabular absolute -top-0.5 -right-0.5 grid size-4 place-items-center rounded-full bg-[var(--accent)] text-caption2 leading-none font-semibold text-[var(--fg-on-accent)]">
              {num(count)}
            </span>
          )}
        </Button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          role="dialog"
          aria-label={t('common.filters')}
          align="end"
          sideOffset={8}
          collisionPadding={12}
          className={cn(MENU_PANEL, 'w-[320px] max-w-[calc(100vw_-_24px)] p-4')}
        >
          <div className="space-y-3" onKeyDown={(event) => event.stopPropagation()}>
            <Select
              ref={firstField}
              label={t('employee.legalEntity')}
              placeholder={t('employees.anyLegalEntity')}
              options={legalEntities}
              value={entity ?? ''}
              onChange={(event) =>
                onChange({ entity: blankToUndefined(event.target.value), department, position })
              }
            />
            <Select
              label={t('employee.department')}
              placeholder={t('employees.anyDepartment')}
              options={departments}
              value={department ?? ''}
              onChange={(event) =>
                onChange({ entity, department: blankToUndefined(event.target.value), position })
              }
            />
            <Select
              label={t('employee.position')}
              placeholder={t('employees.anyPosition')}
              options={positions}
              value={position ?? ''}
              onChange={(event) =>
                onChange({ entity, department, position: blankToUndefined(event.target.value) })
              }
            />

            {count > 0 && (
              <div className="flex justify-end pt-1">
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  onClick={() =>
                    onChange({ entity: undefined, department: undefined, position: undefined })
                  }
                >
                  <X />
                  {t('employees.clearFilters')}
                </Button>
              </div>
            )}
          </div>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

/** «50 / 157 ⌄» — sahifa hajmi / filtrlangan qatorlar soni. */
function PageSizeMenu({
  pageSize,
  total,
  onChange,
}: {
  pageSize: DashboardPageSize
  total: number
  onChange: (next: DashboardPageSize) => void
}) {
  const { t, num } = useLocale()

  return (
    <DropdownMenu.Root modal={false}>
      <DropdownMenu.Trigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="tabular gap-1 px-2.5"
          title={t('attendance.statsPageSize')}
          aria-label={`${t('attendance.statsPageSize')}: ${num(pageSize)} / ${num(total)}`}
        >
          {num(pageSize)} / {num(total)}
          <ChevronDown className="text-[var(--fg-tertiary)]" />
        </Button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content align="end" sideOffset={8} className={cn(MENU_PANEL, 'min-w-[168px]')}>
          <DropdownMenu.Label className={MENU_LABEL}>{t('attendance.statsPageSize')}</DropdownMenu.Label>
          <DropdownMenu.RadioGroup
            value={String(pageSize)}
            onValueChange={(value) => {
              const found = DASHBOARD_PAGE_SIZES.find((size) => String(size) === value)
              if (found !== undefined) onChange(found)
            }}
          >
            {DASHBOARD_PAGE_SIZES.map((size) => (
              <DropdownMenu.RadioItem key={size} value={String(size)} className={MENU_ITEM}>
                <span className="tabular flex-1">{num(size)}</span>
                <DropdownMenu.ItemIndicator>
                  <Check className="size-4 text-[var(--accent-on-soft)]" strokeWidth={3} />
                </DropdownMenu.ItemIndicator>
              </DropdownMenu.RadioItem>
            ))}
          </DropdownMenu.RadioGroup>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

/**
 * Ixcham sahifalash: ‹ 1 2 3 4 5 ›.
 *
 * Umumiy `Pagination` EMAS: u ro'yxat ostidagi to'liq qator («1–50 / 157»
 * yozuvi, 7 katakli oyna). Bu yerda u asboblar qatorida turadi va
 * «50 / 157» allaqachon yonida — shu sababli faqat raqamlar, beshtagacha.
 * Tugma uslubi umumiy komponentniki bilan aynan bir xil.
 */
function CompactPager({
  page,
  pageCount,
  onChange,
  disabled,
}: {
  page: number
  pageCount: number
  onChange: (next: number) => void
  disabled: boolean
}) {
  const { t, num } = useLocale()

  if (pageCount <= 1) return null

  const current = Math.min(Math.max(1, page), pageCount)
  const start = Math.max(1, Math.min(current - 2, pageCount - 4))
  const end = Math.min(pageCount, start + 4)
  const pages = Array.from({ length: end - start + 1 }, (_, index) => start + index)

  return (
    <nav aria-label={t('employees.pagination')} className="flex items-center">
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        disabled={disabled || current === 1}
        onClick={() => onChange(current - 1)}
        title={t('employees.prevPage')}
        aria-label={t('employees.prevPage')}
      >
        <ChevronLeft />
      </Button>

      {pages.map((item) => (
        <button
          key={item}
          type="button"
          disabled={disabled}
          aria-current={item === current ? 'page' : undefined}
          onClick={() => onChange(item)}
          className={cn(
            'tabular grid h-8 min-w-8 place-items-center rounded-sm px-1.5 text-footnote font-medium',
            'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
            'disabled:pointer-events-none disabled:opacity-40',
            item === current
              ? 'bg-[var(--accent)] text-[var(--fg-on-accent)] active:bg-[var(--accent-pressed)]'
              : 'text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)] active:bg-[var(--bg-active)]',
          )}
        >
          {num(item)}
        </button>
      ))}

      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        disabled={disabled || current === pageCount}
        onClick={() => onChange(current + 1)}
        title={t('employees.nextPage')}
        aria-label={t('employees.nextPage')}
      >
        <ChevronRight />
      </Button>
    </nav>
  )
}

/**
 * «≡» — ko'rinadigan ustunlar. F.I.Sh. ro'yxatda turadi, lekin o'chirib
 * bo'lmaydi: ismsiz qator hech narsani anglatmaydi. Tanlov brauzerda
 * eslab qolinadi (`attendance-dashboard-prefs.ts`).
 *
 * `onSelect` da `preventDefault` — menyu har bosishda yopilmasin: odatda
 * bir nechta ustun birdaniga almashtiriladi.
 */
function ColumnsMenu({
  hidden,
  onChange,
}: {
  hidden: ReadonlySet<DashboardColumn>
  onChange: (column: DashboardColumn, visible: boolean) => void
}) {
  const { t } = useLocale()

  return (
    <DropdownMenu.Root modal={false}>
      <DropdownMenu.Trigger asChild>
        <Button
          type="button"
          variant={hidden.size > 0 ? 'soft' : 'ghost'}
          size="icon"
          title={t('attendance.statsColumns')}
          aria-label={t('attendance.statsColumns')}
        >
          <Menu />
        </Button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content align="end" sideOffset={8} className={cn(MENU_PANEL, 'min-w-[208px]')}>
          <DropdownMenu.Label className={MENU_LABEL}>{t('attendance.statsColumns')}</DropdownMenu.Label>

          <DropdownMenu.CheckboxItem checked disabled className={MENU_ITEM}>
            <MenuCheck />
            {t('attendance.statsColName')}
          </DropdownMenu.CheckboxItem>

          {DASHBOARD_COLUMNS.map((column) => (
            <DropdownMenu.CheckboxItem
              key={column}
              checked={!hidden.has(column)}
              onCheckedChange={(checked) => onChange(column, checked === true)}
              onSelect={(event) => event.preventDefault()}
              className={MENU_ITEM}
            >
              <MenuCheck />
              {t(COLUMN_LABEL_KEY[column])}
            </DropdownMenu.CheckboxItem>
          ))}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

/** Belgi uchun doimiy joy — belgilanmagan qator ham bir xil chekinishda boshlanadi. */
function MenuCheck() {
  return (
    <span className="grid size-4 shrink-0 place-items-center">
      <DropdownMenu.ItemIndicator>
        <Check className="size-4 text-[var(--accent-on-soft)]" strokeWidth={3} />
      </DropdownMenu.ItemIndicator>
    </span>
  )
}

/* ═══════════════════════════════════════════════════════ jadval kataklari */

/**
 * Rasm va ism: birinchi qatorda familiya, ostida ism va otasining ismi
 * (Verifix'dagi ko'rinish). HR odamni YUZIDAN taniydi — shu sababli
 * haqiqiy rasm (`EmployeePhoto`), bosh harflar faqat rasm yo'q bo'lsa.
 */
function EmployeeName({
  item,
  linked,
  size = 'md',
}: {
  item: DashboardItem
  linked: boolean
  size?: AvatarSize
}) {
  const [surname = item.fullName, ...rest] = item.fullName.trim().split(/\s+/u)
  const given = rest.join(' ')

  const content: ReactNode = (
    <>
      <EmployeePhoto
        employeeId={item.employeeId}
        photoKey={item.photoKey}
        name={item.fullName}
        size={size}
      />
      <span className="min-w-0">
        <span
          className={cn(
            'truncate-1 block font-semibold',
            linked && 'text-[var(--accent-text)]',
          )}
        >
          {surname}
        </span>
        {given !== '' && (
          <span className="truncate-1 block text-footnote text-[var(--fg-secondary)]">{given}</span>
        )}
      </span>
    </>
  )

  if (!linked) {
    return <span className="flex min-w-0 items-center gap-3">{content}</span>
  }

  return (
    <Link
      to="/xodimlar/$employeeId"
      params={{ employeeId: item.employeeId }}
      // Qatorning o'zi ham kartani ochadi — havola bosilganda ikki marta o'tilmasin
      onClick={(event) => event.stopPropagation()}
      className="press flex min-w-0 items-center gap-3 rounded-md hover:underline"
    >
      {content}
    </Link>
  )
}

/** «08:03» — Toshkent vaqtida; bo'lmasa «—» (masalan hali ketmagan). */
function TimeCell({ value }: { value: string | null }) {
  return (
    <span className={cn('tabular text-subhead', value === null && 'text-[var(--fg-tertiary)]')}>
      {formatPassTime(value)}
    </span>
  )
}

/** Holat: rangli nuqta va matn — «Vaqtida keldi», «33 daqiqa kechikdi», «Kelmadi». */
function StatusText({ item }: { item: DashboardItem }) {
  const { t, num } = useLocale()
  const look = DASHBOARD_STATUS_LOOK[item.status]

  const label =
    item.status === 'Late'
      ? item.lateMinutes !== null && item.lateMinutes > 0
        ? t('attendance.statsRowLate', { n: num(item.lateMinutes) })
        : // Shartnoma bo'yicha kechikkanda daqiqa DOIM bor; bo'lmasa ham «0
          // daqiqa kechikdi» degan yolg'on o'rniga umumiy so'z
          t('attendance.statsRowLateUnknown')
      : t(look.rowKey)

  return (
    <span className={cn('inline-flex items-center gap-2 text-subhead font-medium', look.textClass)}>
      <span aria-hidden className={cn('size-2 shrink-0 rounded-full', look.dotClass)} />
      {label}
    </span>
  )
}

/**
 * Tor ekrandagi karta — jadval qatorining o'zi, ustunlar menyusiga bo'ysunadi.
 */
function EmployeeCard({
  item,
  linked,
  hidden,
}: {
  item: DashboardItem
  linked: boolean
  hidden: ReadonlySet<DashboardColumn>
}) {
  const { t } = useLocale()

  const showTimes = !hidden.has('arrived') || !hidden.has('left')
  const showFooter = showTimes || !hidden.has('status')

  return (
    <Card className="p-4">
      <EmployeeName item={item} linked={linked} size="lg" />

      {!hidden.has('position') && item.positionName !== null && (
        <p className="truncate-2 mt-2 text-footnote text-[var(--fg-secondary)]">{item.positionName}</p>
      )}

      {showFooter && (
        <div className="mt-3 flex flex-wrap items-center justify-between gap-x-4 gap-y-2 border-t border-[var(--border)] pt-3">
          {!hidden.has('status') && <StatusText item={item} />}

          {showTimes && (
            <span className="flex items-center gap-3 text-footnote text-[var(--fg-secondary)]">
              {!hidden.has('arrived') && (
                <span>
                  {t(COLUMN_LABEL_KEY.arrived)}{' '}
                  <span className="tabular font-semibold text-[var(--fg)]">
                    {formatPassTime(item.arrivedAt)}
                  </span>
                </span>
              )}
              {!hidden.has('left') && (
                <span>
                  {t(COLUMN_LABEL_KEY.left)}{' '}
                  <span className="tabular font-semibold text-[var(--fg)]">
                    {formatPassTime(item.leftAt)}
                  </span>
                </span>
              )}
            </span>
          )}
        </div>
      )}
    </Card>
  )
}

function blankToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}
