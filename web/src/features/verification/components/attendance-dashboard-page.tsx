import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { getRouteApi, Link } from '@tanstack/react-router'
import {
  ArrowDownAZ,
  ArrowUpZA,
  CalendarRange,
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
  Rows3,
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
import { EMPLOYEES_VIEW, REPORTS_EXPORT } from '@/features/employees/lib/employee-presentation'
import { useListExport } from '@/features/reports/api/list-export'
import { ListExportButton, ListExportError } from '@/features/reports/components/list-export'
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
  TextField,
  dataColumnHelper,
  type AvatarSize,
  type DataColumn,
  type SelectOption,
} from '@/shared/ui'

import {
  attendanceExportParams,
  type BirthdayFilters,
  type DashboardItem,
  type DashboardFilters,
  type DashboardSort,
  type DashboardStatus,
  type TrendDay,
  type TrendFilters,
} from '../api/attendance-api'
import {
  useAttendanceDashboard,
  useAttendanceTrend,
  useFormerEmployeeAlerts,
} from '../api/attendance-queries'
import {
  DASHBOARD_COLUMNS,
  useDashboardPrefs,
  type DashboardColumn,
} from '../lib/attendance-dashboard-prefs'
import {
  ATTENDANCE_VIEW,
  DASHBOARD_STATUSES,
  DASHBOARD_STATUS_LOOK,
  DEFAULT_TREND_SERIES,
  dashboardPercent,
  formatPassTime,
  type DashboardView,
} from '../lib/attendance-presentation'
import {
  DASHBOARD_PAGE_SIZES,
  DASHBOARD_PRESETS,
  DEFAULT_DASHBOARD_PAGE_SIZE,
  dashboardPeriod,
  dashboardPinOf,
  hasPinValues,
  isRangeInverted,
  isRangeTooLong,
  periodHasToday,
  readIsoDate,
  samePin,
  shiftIsoDate,
  type DashboardPageSize,
  type DashboardPeriod,
  type DashboardPreset,
  type DashboardSearch,
} from '../lib/attendance-search'
import {
  DashboardChart,
  DashboardChartSkeleton,
  DashboardLegend,
  DashboardTrendChart,
  DashboardTrendSkeleton,
  DashboardTrendToggles,
  type DashboardSlice,
} from './attendance-dashboard-chart'
import { BirthdaysCard } from './attendance-birthdays-card'
import { DeviceHealthBanners } from './attendance-device-health'
import { AttendanceExitTime } from './attendance-exit-time'
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

/** Shu sabab bilan — grafik uchun. */
const EMPTY_DAYS: TrendDay[] = []

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

/*
 * ⭐ FILTR USTUNI QACHON SIG'ADI — ekran kengligi emas, SAHIFA MAYDONINING
 * kengligi (container query, `@container/page`).
 *
 * Birinchi versiyada ustun `xl` (1280 px ekran) dan chiqardi va jadvalni
 * EZIB qo'ydi (egasi 2026-09-18 da ushladi). Hisob: 1512 px ekranda ochiq
 * sidebar (264) va chekkalardan keyin maydon ~1170 px; ustun 344 ni,
 * diagramma 300 ni oladi — jadvalga ~525 px qoladi, uning ustunlari esa
 * kamida ~704 px so'raydi (F.I.Sh. 200, lavozim 140, ikki vaqt 92+92,
 * holat 180). Ekran kengligi bilan tanlab bo'lmaydi: sidebar yig'ilsa
 * maydon 188 px kengayadi, media query esa buni ko'rmaydi.
 *
 * Shu sababli ikki qoida:
 *   · ustun faqat maydon ≥ 1100 px bo'lganda chiziladi (qolgan ~756 px
 *     jadvalga yetadi);
 *   · ustun chizilganda diagramma jadvalning CHAPIDAN TEPASIGA ko'chadi
 *     (gorizontal tasma: doira + legenda) — uchta ustun yonma-yon sig'maydi.
 * Tor maydonda hammasi avvalgidek: diagramma chapda, filtrlar ochiluvchi
 * panelda.
 *
 * Qiymat sinf nomlarining ICHIDA takrorlanadi (Tailwind sinfni matndan
 * o'qiydi, o'zgaruvchidan yig'ib bo'lmaydi) — o'zgartirilsa, `@min-[1100px]`
 * ni fayl bo'ylab qidiring.
 */

/**
 * Davr presetlarining tarjima kalitlari — tanlagich ham, sarlavhadagi
 * yozuv ham SHU xaritadan o'qiydi: ro'yxatda bir nom, tugmada boshqasi
 * bo'lib qolmasin.
 */
const PERIOD_LABEL_KEY: Readonly<Record<DashboardPreset, string>> = {
  today: 'attendance.statsToday',
  yesterday: 'attendance.statsPeriodYesterday',
  last7: 'attendance.statsPeriodLast7',
  prevWeek: 'attendance.statsPeriodPrevWeek',
  firstHalf: 'attendance.statsPeriodFirstHalf',
  secondHalf: 'attendance.statsPeriodSecondHalf',
  last30: 'attendance.statsPeriodLast30',
  thisMonth: 'attendance.statsPeriodThisMonth',
  prevMonth: 'attendance.statsPeriodPrevMonth',
  custom: 'attendance.statsPeriodCustom',
}

/**
 * Davomat statistikasi — Verifix'dagi «Статистика посещений сотрудников»
 * ekranining o'rnini bosadi (egasining talabi, 2026-09-17: «asosiy sahifa
 * rasmdagidek bo'lsin»; 2026-09-18 da davr ustuni bilan kengaytirildi —
 * `docs/SPEC-verifikatsiya-panel.md`).
 *
 * IKKI REJIM va ular ikki ENDPOINT:
 *   · BITTA KUN (`GET /dashboard`) — diagramma yoki diagrammasiz ro'yxat,
 *     tepasida holat chiplari, ostida xodimlar jadvali;
 *   · DAVR (`GET /dashboard/trend`) — faqat kunlik chiziqli grafik.
 *     Jadval, chiplar, qidiruv va sahifalash bu rejimda YO'Q (egasining
 *     qarori: «faqat kunlik grafik») — ular bir kunning savoli.
 * Rejimni `dashboardPeriod` hal qiladi, sahifa uni tekshirmaydi.
 *
 * Bitta keng karta:
 *   · sarlavha qatori — nom, kun tanlagich, ko'rinish turi, yangilash;
 *   · chapda — diagramma (markazda jami) va legenda;
 *   · o'ngda — asboblar qatori, holat chiplari va xodimlar jadvali;
 *   · `xl` dan boshlab kartaning O'NGIDA doimiy filtr ustuni turadi.
 * `lg` dan tor ekranda diagramma kartasi jadval ustiga tushadi va jadval
 * kartalarga aylanadi; `xl` dan tor ekranda filtr ustuni chizilmaydi va
 * filtrlar avvalgidek ochiluvchi panelda qoladi.
 *
 * Holat qayerda:
 *   · davr, qidiruv, holat, firma/bo'lim/lavozim, KO'RINISH TURI, saralash,
 *     sahifa va uning hajmi — MANZILDA (`validateDashboardSearch`);
 *   · qadalgan filtrlar va ustunlar — brauzerda, foydalanuvchi bo'yicha
 *     (`attendance-dashboard-prefs.ts`);
 *   · grafikda qaysi qatorlar yoqilgani — sahifaning o'zida: bu chizmani
 *     o'qishning bir martalik usuli, filtr emas.
 *
 * ⭐ Tanlangan davr BUGUNNI qamrasa sahifa 30 soniyada bir yangilanadi va
 * hech qachon BO'SHAMAYDI (`useAttendanceDashboard` izohi). O'tgan davr
 * so'ralmaydi.
 */
export function AttendanceDashboardPage() {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission, user, isAttendanceScoped } = useAuth()

  const canView = hasPermission(ATTENDANCE_VIEW)
  /*
    Xodim kartasi `employees.view` bilan himoyalangan (server). Ruxsat
    bo'lmasa ism HAVOLA EMAS — oddiy matn: bosiladigan, lekin 403 ga olib
    boradigan havola bajarilmaydigan va'da bo'lardi.
  */
  const canOpenEmployee = hasPermission(EMPLOYEES_VIEW) && !isAttendanceScoped
  // Jadvalning Excel eksporti — statistikani ko'rish VA `reports.export` (SPEC-excel-eksport §1)
  const canExport = canView && hasPermission(REPORTS_EXPORT)

  const titleId = useId()
  const search = route.useSearch()
  const navigate = route.useNavigate()

  /*
    «Bugun» har renderda qayta olinadi: sahifa yarim tundan keyin ham ochiq
    turadi va keyingi 30 soniyalik yangilanish o'zi yangi kunga o'tadi
    (manzilda sana yo'q — `DashboardSearch.date` izohi).
  */
  const today = todayIso()

  /*
    Manzildagi preset → aniq sanalar. Butun sahifa faqat SHU qiymatga
    qaraydi: «qaysi endpoint», «kun tanlagich ko'rinadimi», «jadval
    bormi» — hammasi shundan (`dashboardPeriod` izohi).
  */
  const period = dashboardPeriod(search, today)
  const range = period.kind === 'range'
  const live = periodHasToday(period, today)

  /*
    Davrning sanalari — so'rov kaliti uchun ODDIY qiymatga keltirilgan.
    `useMemo` bog'liqliklarida shartli ifoda yozib bo'lmaydi (lint
    qoidasi) va har render yangi obyekt bo'lgan `period` ni bog'liqlik
    qilish keshni har safar bekor qilardi.
  */
  const dayDate = period.kind === 'day' ? period.date : null
  const rangeFrom = period.kind === 'range' ? period.from : null
  const rangeTo = period.kind === 'range' ? period.to : null

  const q = search.q ?? ''
  const status = search.status ?? null
  const sort: DashboardSort = search.sort ?? 'name'
  const pageSize: DashboardPageSize = search.size ?? DEFAULT_DASHBOARD_PAGE_SIZE
  const page = search.page ?? 1
  const view: DashboardView = search.view ?? 'donut'

  /**
   * Manzilni yangilash. Qidiruv, filtr va saralash — `replace` (bitta
   * ekran ichidagi aniqlashtirish); davr va sahifa — `push`: ular haqiqiy
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

  /*
    Grafikda qaysi qatorlar chizilishi. Sukut — uchta kundalik savol
    (`DEFAULT_TREND_SERIES`). Manzilda TURMAYDI: bu chizmani o'qishning
    usuli, ko'rsatiladigan ma'lumot emas — havolani ochgan odam baribir
    o'ziga qulayini yoqadi.
  */
  const [series, setSeries] = useState<ReadonlySet<DashboardStatus>>(
    () => new Set(DEFAULT_TREND_SERIES),
  )

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

    /*
      ⚠️ Manzilda ANIQ kun bo'lsa (`?date=` — kun tanlagichidan olingan
      havola), qadalgan DAVR qo'llanmaydi. Aks holda «16-sentabr» degan
      havola qadalgan «Oxirgi 7 kun» tufayli boshqa ekranni ochardi:
      preset kunni yutib yuboradi (`dashboardPeriod`). Qolgan qadalgan
      maydonlar — firma, bo'lim, lavozim, tartib, ko'rinish — baribir
      qo'llanadi: ular kun bilan ziddiyatga kirmaydi.
    */
    const applied =
      search.date === undefined ? pin : { ...pin, period: undefined, from: undefined, to: undefined }

    setSearch((previous) => ({ ...previous, ...applied, page: undefined }))
    // Faqat birinchi kirishda — keyingi o'zgarishlar foydalanuvchining o'z qarori
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Catalogs are closed to a «Davomat mas'uli»; their org filters are hidden below
  const options = useFilterOptions(!isAttendanceScoped)

  /*
    Jadval filtri SAHIFASIZ — ekrandagi jadval ham, Excel eksporti ham AYNAN
    shundan: kun, firma/bo'lim/lavozim, holat chipi, qidiruv va tartib.
    Sahifa raqami va hajmi faqat ekranga qo'shiladi (`employees-page.tsx`
    dagi `listFilters` naqshi).
  */
  const listFilters = useMemo<Omit<DashboardFilters, 'page' | 'pageSize'>>(
    () => ({
      // Bugun berilmasa server o'zi oladi — yarim tundan keyin kalit ham,
      // javob ham o'zi yangi kunga o'tadi. Shu sababli bu yerda AYNAN
      // «bugun emas» shart tekshiriladi, `search.date` emas: «Kecha»
      // preseti ham sanani yuborishi kerak.
      date: dayDate !== null && dayDate !== today ? dayDate : undefined,
      search: q.trim() === '' ? undefined : q,
      legalEntityId: search.entity,
      departmentId: search.department,
      positionId: search.position,
      status: search.status,
      sort: search.sort,
    }),
    [
      dayDate,
      today,
      q,
      search.entity,
      search.department,
      search.position,
      search.status,
      search.sort,
    ],
  )

  const filters = useMemo<DashboardFilters>(
    () => ({ ...listFilters, page, pageSize }),
    [listFilters, page, pageSize],
  )

  // Xato mutatsiyada qoladi — keyingi bosish uni o'zi tozalaydi (`employees-page.tsx`)
  const exportList = useListExport()

  /*
    Davr rejimida ishlatiladi. Kun rejimida ham hisoblanadi (hook shartli
    chaqirilmaydi), lekin so'rov `enabled: false` bilan YUBORILMAYDI —
    ikkita og'ir hisob yonma-yon ketmasligi kerak.
  */
  const trendFilters = useMemo<TrendFilters>(
    () => ({
      from: rangeFrom ?? today,
      to: rangeTo ?? today,
      legalEntityId: search.entity,
      departmentId: search.department,
      positionId: search.position,
    }),
    [
      rangeFrom,
      rangeTo,
      today,
      search.entity,
      search.department,
      search.position,
    ],
  )

  /*
    Tug'ilgan kunlar paneli — DAVRSIZ, ataylab (SPEC-panel §E). Faqat
    org-filtr: davr qo'shilsa, har preset almashtirilganda AYNAN bir xil
    javob qaytadan so'ralardi (kesh kaliti `attendance-queries.ts` da).
  */
  const birthdayFilters = useMemo<BirthdayFilters>(
    () => ({
      legalEntityId: search.entity,
      departmentId: search.department,
      positionId: search.position,
    }),
    [search.entity, search.department, search.position],
  )

  const query = useAttendanceDashboard(filters, live, canView && !range)
  const trend = useAttendanceTrend(trendFilters, live, canView && range)

  /*
    Ogohlantirish statistikaning O'Z javobidan emas, alohida so'rovdan
    (`useFormerEmployeeAlertsQuery` izohi): menyudagi qizil nishon bilan
    bitta kesh yozuvi, va statistikaning og'ir hisobi yiqilganda ham
    xavfsizlik hodisasi ekrandan yo'qolmaydi.
  */
  const alerts = useFormerEmployeeAlerts(canView)

  const data = query.data
  const trendData = trend.data

  /** Ekranda hozir qaysi so'rov turibdi — holat bayroqlari faqat undan o'qiladi. */
  const active = range ? trend : query

  /*
    Oxirgi sahifadan narida qolib ketmaslik (`employees-page.tsx` dagi
    izoh): filtr torayganda yoki eski havolada `?page=7` turganda.
    `isPlaceholderData` da eski javobning `pageCount` iga qarab sakralmaydi.
  */
  useEffect(() => {
    if (range || data === undefined || query.isPlaceholderData) return

    const lastPage = Math.max(1, data.pageCount)
    if (page <= lastPage) return

    setSearch((previous) => ({ ...previous, page: lastPage <= 1 ? undefined : lastPage }))
  }, [range, data, query.isPlaceholderData, page, setSearch])

  const columns = useMemo<Array<DataColumn<DashboardItem>>>(() => {
    const list: Array<DataColumn<DashboardItem>> = [
      // ⭐ Jadvalda BIR QATOR (egasi 2026-10-22): familiya qalin, ism yonida
      helper.accessor('fullName', {
        header: t('attendance.statsColName'),
        cell: ({ row }) => <EmployeeName item={row.original} linked={canOpenEmployee} inline />,
      }),
    ]

    if (!hidden.has('position')) {
      list.push(
        helper.accessor('positionName', {
          header: t(COLUMN_LABEL_KEY.position),
          enableSorting: false,
          cell: ({ row }) => (
            <span className="text-[var(--fg-secondary)]">{row.original.positionName ?? '—'}</span>
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
          cell: ({ row }) => <TimeCell value={row.original.leftAt} auto={row.original.leftAuto} />,
        }),
      )
    }

    if (!hidden.has('status')) {
      list.push(
        helper.accessor('status', {
          header: t(COLUMN_LABEL_KEY.status),
          enableSorting: false,
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

    /*
      Kun tanlagich presetni BEKOR QILADI: «Kecha» dan bir kun orqaga
      surilgan ekran endi «kecha» emas. Preset joyida qolsa, manzil
      o'zi bilan ziddiyatda bo'lardi.
    */
    setSearch(
      (previous) => ({
        ...previous,
        period: undefined,
        from: undefined,
        to: undefined,
        date: clamped === today ? undefined : clamped,
        page: undefined,
      }),
      'push',
    )
  }

  /** Davr preseti (`custom` da — ikkita sana bilan). */
  const changePeriod = (next: DashboardPreset, custom?: { from: string; to: string }) => {
    setSearch(
      (previous) => ({
        ...previous,
        period: next === 'today' ? undefined : next,
        from: next === 'custom' ? custom?.from : undefined,
        to: next === 'custom' ? custom?.to : undefined,
        // Bitta kun tanlagichining qiymati presetga qo'shilib qolmasin
        date: undefined,
        page: undefined,
      }),
      'push',
    )
  }

  /** Bo'lak, ustun, legenda yoki chip: birinchi bosish — filtr, ikkinchisi — olib tashlash. */
  const toggleStatus = (next: DashboardStatus) => {
    setSearch((previous) => ({
      ...previous,
      status: previous.status === next ? undefined : next,
      page: undefined,
    }))
  }

  const clearStatus = () => {
    setSearch((previous) => ({ ...previous, status: undefined, page: undefined }))
  }

  const changeOrg = (patch: Pick<DashboardSearch, 'entity' | 'department' | 'position'>) => {
    setSearch((previous) => ({ ...previous, ...patch, page: undefined }))
  }

  const changeView = (next: DashboardView) => {
    setSearch((previous) => ({ ...previous, view: next === 'donut' ? undefined : next }))
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

  const toggleSeries = (next: DashboardStatus) => {
    setSeries((previous) => {
      const updated = new Set(previous)
      if (!updated.delete(next)) updated.add(next)
      return updated
    })
  }

  const refresh = () => void active.refetch()

  /* ───────────────────────────────────────────────── ko'rinish */

  const firstLoad = active.isPending
  const loadFailed = active.isError && active.data === undefined
  /** Davr, sahifa yoki filtr almashdi — eski surat turibdi, yangisi kelmoqda. */
  const switching = active.isPlaceholderData && active.isFetching

  const items = data?.items ?? EMPTY_ITEMS
  const total = data?.total ?? 0
  const totalItems = data?.totalItems ?? 0
  const pageCount = Math.max(1, data?.pageCount ?? 1)
  const days = trendData?.days ?? EMPTY_DAYS
  const asOf = range ? trendData?.asOf : data?.asOf

  const slices: DashboardSlice[] = DASHBOARD_STATUSES.map((item) => {
    const count = data?.counts[DASHBOARD_STATUS_LOOK[item].count] ?? 0
    return { status: item, count, percent: dashboardPercent(count, total) }
  })

  const orgCount = [search.entity, search.department, search.position].filter(
    (value) => value !== undefined,
  ).length
  const filtered = q.trim() !== '' || status !== null || orgCount > 0
  const pinnable = pinned || prefs.pin !== null || hasPinValues(currentPin)

  /** Diagramma ustuni chizilmaydigan ikki hol: `list` ko'rinishi va davr grafigi. */
  const wide = range || view === 'list'

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
    <div className="@container/page space-y-6">
      {/*
        ⚠️ ENG TEPADA, kartadan YUQORIDA (SPEC §8.4, egasining talabi):
        xavfsizlik hodisasi diagramma ostida «yana bitta raqam» bo'lib
        qolmasligi kerak. Faqat ogohlantirish BOR bo'lsa chiziladi.
      */}
      {alerts.length > 0 && (
        <FormerEmployeeAlertPanel alerts={alerts} canOpenEmployee={canOpenEmployee} />
      )}

      {/*
        Qurilmalar sog'lig'i (SPEC §8.4, §8.7, SPEC-agent §L): agent yoki
        terminal uzilishi — qizil, soat og'ishi — sariq, hech bir terminal
        ulanmagan — ma'lumot. «Hozir ishda» taxtasi Statistika bilan
        almashtirilganda bular yo'qolib qolgan edi. Ikkala rejimda ham:
        davr rejimida ma'lumot `/dashboard/trend` javobidan keladi.
      */}
      <DeviceHealthBanners health={range ? (trendData?.deviceHealth ?? undefined) : (data?.deviceHealth ?? undefined)} />

      {/* Keyingi yangilanish yiqildi — eski surat joyida qoladi, sabab tepada */}
      {active.isError && active.data !== undefined && (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {errorMessage(problemCode(active.error))}
        </Alert>
      )}

      {/*
        Keng maydonda: chapda karta, o'ngda DOIMIY filtr ustuni (Verifix
        joylashuvi, SPEC C1). Ustun sahifa bilan birga aylanadi va tepada
        yopishib turadi. Qachon «keng» — fayl boshidagi izoh.

        Tor maydonda ustun UMUMAN chizilmaydi (`hidden`): filtrlar
        avvalgidek asboblar qatoridagi ochiluvchi panelda qoladi, ya'ni
        telefon ko'rinishi o'zgarmaydi.
      */}
      <div className="@min-[1100px]/page:grid @min-[1100px]/page:grid-cols-[minmax(0,1fr)_320px] @min-[1100px]/page:items-start @min-[1100px]/page:gap-6">
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

          ⚠️ Ustunlar `lg:col-span-full` bilan cho'ziladi, `col-span-2`
          bilan emas: `list` va davr ko'rinishida grid BITTA ustunli
          bo'ladi va `col-span-2` yo'q joydan ikkinchi ustun ochib yuborardi.
        */}
        <section
          aria-labelledby={titleId}
          className={cn(
            'space-y-4',
            'lg:grid lg:space-y-0',
            'lg:overflow-clip lg:rounded-lg lg:border lg:border-[var(--border)] lg:bg-[var(--bg-elevated)] lg:shadow-card',
            // Filtr ustuni chizilganda diagramma jadval TEPASIGA o'tadi
            // (fayl boshidagi izoh) — grid shunda ham bitta ustunli.
            wide
              ? 'lg:grid-cols-[minmax(0,1fr)]'
              : 'lg:grid-cols-[300px_minmax(0,1fr)] @min-[1100px]/page:grid-cols-[minmax(0,1fr)]',
          )}
        >
          <Card className="lg:contents">
            <header className="flex flex-wrap items-center justify-between gap-x-4 gap-y-3 px-4 pt-4 sm:px-5 sm:pt-5 lg:col-span-full lg:border-b lg:border-[var(--border)] lg:pb-4">
              <div className="flex min-w-0 items-center gap-3">
                <IconTile icon={ChartPie} tone="brand" size="lg" />
                <div className="min-w-0">
                  <h1 id={titleId} className="text-title2 leading-tight font-semibold tracking-tight">
                    {t('attendance.statsTitle')}
                  </h1>
                  <p className="mt-1 text-footnote text-[var(--fg-secondary)]">
                    {asOf === undefined
                      ? t('common.loading')
                      : t('attendance.refreshedAt', {
                          // Server lahzasi — brauzer soati emas: eski javob
                          // «hozirgina yangilandi» deb yolg'on gapirmasin
                          time: formatPassTime(asOf),
                        })}
                  </p>
                </div>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                {/* Kun tanlagich faqat BITTA kunda ma'noli (SPEC C2) */}
                {period.kind === 'day' && (
                  <DateStepper value={period.date} today={today} onChange={changeDate} />
                )}

                {/* Filtr ustuni yo'q maydonda davr tanlagich shu yerda */}
                <div className="@min-[1100px]/page:hidden">
                  <PeriodPicker period={period} today={today} onChange={changePeriod} />
                </div>

                {/* Davrda diagramma bitta — tanlaydigan narsa yo'q (SPEC C5) */}
                {!range && <ViewPicker value={view} onChange={changeView} />}

                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  onClick={refresh}
                  title={t('attendance.statsRefresh')}
                  aria-label={t('attendance.statsRefresh')}
                >
                  <RefreshCw className={cn(active.isFetching && 'animate-spin')} />
                </Button>
              </div>
            </header>

            {loadFailed ? (
              /*
                Birinchi yuklash yiqildi — bo'sh diagramma («0 xodim») emas,
                sabab va qayta urinish: nol bu yerda yolg'on bo'lardi.
              */
              <AttendanceLoadError
                className="lg:col-span-full"
                error={active.error}
                onRetry={refresh}
                retrying={active.isFetching}
              />
            ) : range ? (
              <div
                className={cn(
                  'px-4 pt-2 pb-4 sm:px-5 sm:pb-5 lg:col-span-full lg:pt-5',
                  'transition-opacity duration-200 ease-[var(--ease-ios)]',
                  switching && 'opacity-60',
                )}
                aria-busy={switching || firstLoad}
              >
                {firstLoad ? (
                  <DashboardTrendSkeleton />
                ) : (
                  <>
                    <DashboardTrendChart days={days} series={series} />
                    <div className="mt-4">
                      <p className="pb-2 text-caption2 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
                        {t('attendance.statsTrendSeries')}
                      </p>
                      <DashboardTrendToggles series={series} onToggle={toggleSeries} />
                    </div>
                  </>
                )}
              </div>
            ) : view === 'list' ? null : (
              <div
                className={cn(
                  'px-4 pt-2 pb-4 sm:px-5 sm:pb-5',
                  // Uzun jadval aylantirilganda diagramma va legenda ko'z oldida
                  // qoladi — legendani bosib filtrlash uchun tepaga qaytish shart emas
                  'lg:sticky lg:top-0 lg:self-start lg:pt-5',
                  /*
                    Keng maydonda diagramma jadval TEPASIDA — endi u tasma:
                    doira chapda, legenda o'ngda. `sticky` bu yerda O'CHADI:
                    jadval ustidagi yopishqoq tasma aylantirilganda qatorlarni
                    yopib qo'yardi.
                  */
                  '@min-[1100px]/page:static @min-[1100px]/page:flex @min-[1100px]/page:items-center @min-[1100px]/page:gap-8',
                )}
              >
                {firstLoad ? (
                  <DashboardChartSkeleton />
                ) : (
                  <>
                    {/*
                      Tasmada doira 300 px da qoladi (yonida legenda), ustunlar
                      esa BUTUN kenglikni oladi: ular o'zi yorliqlangan va
                      legenda ularning ikkinchi nusxasi bo'lardi.
                    */}
                    <div
                      className={cn(
                        view === 'bars'
                          ? '@min-[1100px]/page:min-w-0 @min-[1100px]/page:flex-1'
                          : '@min-[1100px]/page:w-[300px] @min-[1100px]/page:shrink-0',
                      )}
                    >
                      <DashboardChart
                        type={view}
                        slices={slices}
                        total={total}
                        active={status}
                        onToggle={toggleStatus}
                      />
                    </div>

                    {/*
                      Legenda faqat DOIRAVIYDA: doira bo'laklarida nom yo'q, ustunlar
                      esa nomi, soni va foizini o'zi yozadi (`StatusBars` izohi).
                    */}
                    {view === 'donut' && (
                      <div className="mt-3 @min-[1100px]/page:mt-0 @min-[1100px]/page:min-w-0 @min-[1100px]/page:flex-1">
                        <DashboardLegend slices={slices} active={status} onToggle={toggleStatus} />
                      </div>
                    )}
                  </>
                )}
              </div>
            )}
          </Card>

          {!loadFailed && !range && (
            <div
              className={cn(
                'min-w-0 space-y-3 lg:space-y-0',
                view !== 'list' &&
                  'lg:border-l lg:border-[var(--border)] @min-[1100px]/page:border-t @min-[1100px]/page:border-l-0',
              )}
            >
              <Card className="lg:rounded-none lg:border-0 lg:border-b lg:border-[var(--border)] lg:bg-transparent lg:shadow-none">
                <div className="flex flex-wrap items-center gap-2 p-3 sm:px-4">
                  {/*
                    Qidiruv 420 px dan cho'zilmaydi: sahifa endi to'liq kenglikda
                    (`jonli.tsx`) va katta monitorda maydon ~1000 px bo'lib, asboblar
                    ko'zdan uzoqlashib ketardi. Asboblar `ml-auto` bilan o'ngga.
                  */}
                  <div className="min-w-[180px] flex-1 basis-[220px] sm:max-w-[420px]">
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

                  <div className="ml-auto flex flex-wrap items-center gap-1">
                    {/* Filtr ustuni bor maydonda bu tugma ortiqcha */}
                    <div className={cn('@min-[1100px]/page:hidden', isAttendanceScoped && 'hidden')}>
                      <FilterPopover
                        entity={search.entity}
                        department={search.department}
                        position={search.position}
                        legalEntities={options.legalEntities}
                        departments={options.departments}
                        positions={options.positions}
                        onChange={changeOrg}
                      />
                    </div>

                    {/*
                      Eksport — «Filtrlar» yonida: fayl aynan shu kun, filtr, holat
                      chipi va qidiruvga tushadi. Faqat kun rejimida — davr
                      grafigida bu jadval umuman chizilmaydi.
                    */}
                    {canExport && (
                      <ListExportButton
                        onClick={() =>
                          exportList.mutate({
                            path: '/api/attendance/dashboard/export',
                            params: attendanceExportParams(listFilters),
                            slug: 'davomat_statistika',
                          })
                        }
                        pending={exportList.isPending}
                        // Qator zich: yorliq faqat juda keng ekranda, aks holda asboblar ikkinchi qatorga tushardi
                        labelClassName="hidden 2xl:inline"
                      />
                    )}

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

                    <ColumnsMenu hidden={hidden} onChange={prefs.setColumnVisible} />
                  </div>
                </div>

                <ListExportError error={exportList.error} className="px-3 pb-3 sm:px-4" />

                {/*
                  Holat chiplari — UCHALA ko'rinishda ham (SPEC C4).
                  Sanoqlar `counts` dan, ya'ni qidiruv va holat filtridan
                  OLDINGI to'plamdan: qidiruvga bitta ism yozilgani
                  «Kechikkanlar 1» degan yolg'onga aylanmasligi kerak.
                */}
                <StatusChipRow
                  total={total}
                  slices={slices}
                  active={status}
                  loading={firstLoad}
                  onSelect={toggleStatus}
                  onClear={clearStatus}
                />
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
                              // Kartaning DAVOMAT bo'limi ochiladi: bu
                              // ekrandan odam «bugun nega kelmadi» degan
                              // savol bilan chiqadi, shartnomasi bilan emas.
                              search: { tab: 'attendance' },
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

        <FilterColumn
          period={period}
          today={today}
          birthdays={birthdayFilters}
          canView={canView}
          canOpenEmployee={canOpenEmployee}
          entity={search.entity}
          department={search.department}
          position={search.position}
          legalEntities={options.legalEntities}
          departments={options.departments}
          positions={options.positions}
          orgCount={orgCount}
          refreshing={active.isFetching}
          onPeriod={changePeriod}
          onOrg={changeOrg}
          onRefresh={refresh}
        />
      </div>
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

const VIEWS: ReadonlyArray<{ value: DashboardView; labelKey: string; icon: LucideIcon }> = [
  { value: 'donut', labelKey: 'attendance.statsChartDonut', icon: ChartPie },
  { value: 'bars', labelKey: 'attendance.statsChartBars', icon: ChartBar },
  { value: 'list', labelKey: 'attendance.statsChartList', icon: Rows3 },
]

/**
 * Ko'rinish turi — ochiluvchi ro'yxat va «✕» (doiraviyga qaytarish).
 *
 * «✕» faqat doiraviydan boshqa tur tanlanganda chiziladi: sukut holatida
 * u hech narsani o'zgartirmaydigan tugma bo'lib qolardi.
 *
 * Uchinchi band — `list` — diagrammani UMUMAN olib tashlaydi (SPEC C3).
 * Shu sababli tugmaning nomi «Diagramma turi» emas, «Ko'rinish».
 */
function ViewPicker({
  value,
  onChange,
}: {
  value: DashboardView
  onChange: (next: DashboardView) => void
}) {
  const { t } = useLocale()
  const current = VIEWS.find((item) => item.value === value) ?? VIEWS[0]
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
            aria-label={`${t('attendance.statsView')}: ${t(current.labelKey)}`}
          >
            <CurrentIcon />
            <span className="hidden sm:inline">{t(current.labelKey)}</span>
            <ChevronDown className="text-[var(--fg-tertiary)]" />
          </Button>
        </DropdownMenu.Trigger>

        <DropdownMenu.Portal>
          <DropdownMenu.Content align="end" sideOffset={8} className={cn(MENU_PANEL, 'min-w-[220px]')}>
            <DropdownMenu.Label className={MENU_LABEL}>{t('attendance.statsView')}</DropdownMenu.Label>
            <DropdownMenu.RadioGroup
              value={value}
              onValueChange={(next) => {
                const found = VIEWS.find((item) => item.value === next)
                if (found !== undefined) onChange(found.value)
              }}
            >
              {VIEWS.map((item) => {
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

/* ═══════════════════════════════════════════════════════════ davr */

/**
 * Davr tanlagich: o'nta preset va «Ixtiyoriy oraliq» ning ikki sanasi
 * (SPEC C2, Verifix ekranidagi tartib).
 *
 * ⭐ Preset bosilishi bilan QO'LLANADI va panel yopiladi — tanlovni
 * tasdiqlatadigan ikkinchi bosish HR ning kunlik ishida ortiqcha.
 * Yagona istisno — «Ixtiyoriy oraliq»: u ikkita sana so'raydi, ya'ni
 * yarim to'ldirilgan holatda qo'llansa manzil ma'nosiz bo'lardi. Shu
 * sababli unda «Qo'llash» / «Bekor qilish» bor.
 *
 * Chegaralar tugma bosilishidan OLDIN tekshiriladi (`isRangeInverted`,
 * `isRangeTooLong`) — serverning 400 javobini kutib, keyin uni tarjima
 * qilib ko'rsatishdan ko'ra tushunarliroq (`attendance-search.ts` dagi
 * bir xil mulohaza). Server tekshiruvi baribir joyida qoladi.
 */
function PeriodPicker({
  period,
  today,
  onChange,
  full = false,
}: {
  period: DashboardPeriod
  today: string
  onChange: (preset: DashboardPreset, custom?: { from: string; to: string }) => void
  /** Filtr ustunida tugma butun kenglikni oladi. */
  full?: boolean
}) {
  const { t, date } = useLocale()
  /*
    Chegara xatolari SERVERNING kodlari bilan aytiladi
    (`davomat.davr_notogri`, `davomat.davr_uzun`) — hisobotlar sahifasidagi
    bilan AYNAN bir xil yo'l. Yangi tarjima kaliti ochilsa, bitta xato
    ikki joyda ikki xil matn bilan chiqib ketardi.
  */
  const errorMessage = useErrorMessage()

  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(() => periodBounds(period))

  /*
    Panel har ochilganda joriy davrdan boshlanadi: oldingi, qo'llanmagan
    urinishning sanalari qolib ketsa, foydalanuvchi ko'rmagan oraliq
    «Qo'llash» bilan birdan ishlab ketardi.
  */
  const openChange = (next: boolean) => {
    setOpen(next)
    if (next) {
      setDraft(periodBounds(period))
      setEditing(period.preset === 'custom')
    }
  }

  const inverted = isRangeInverted(draft.from, draft.to)
  const tooLong = !inverted && isRangeTooLong(draft.from, draft.to)
  const invalid = draft.from === '' || draft.to === '' || inverted || tooLong

  const label =
    period.preset === 'custom'
      ? period.kind === 'day'
        ? date(period.date)
        : `${date(period.from)} — ${date(period.to)}`
      : t(PERIOD_LABEL_KEY[period.preset])

  return (
    <DropdownMenu.Root modal={false} open={open} onOpenChange={openChange}>
      <DropdownMenu.Trigger asChild>
        <Button
          type="button"
          pill={!full}
          className={cn('data-[state=open]:border-[var(--accent)]', full && 'w-full justify-between')}
          aria-label={`${t('attendance.statsPeriod')}: ${label}`}
        >
          <span className="flex min-w-0 items-center gap-2">
            <CalendarRange />
            <span className="truncate">{label}</span>
          </span>
          <ChevronDown className="text-[var(--fg-tertiary)]" />
        </Button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align="end"
          sideOffset={8}
          collisionPadding={12}
          className={cn(MENU_PANEL, 'w-[264px] max-w-[calc(100vw_-_24px)]')}
        >
          <DropdownMenu.Label className={MENU_LABEL}>{t('attendance.statsPeriod')}</DropdownMenu.Label>

          <DropdownMenu.RadioGroup
            value={period.preset}
            onValueChange={(next) => {
              const found = DASHBOARD_PRESETS.find((preset) => preset === next)
              if (found === undefined) return

              // «Ixtiyoriy oraliq» panelni YOPMAYDI — u hali sana so'raydi
              if (found === 'custom') {
                setEditing(true)
                return
              }

              onChange(found)
              setOpen(false)
            }}
          >
            {DASHBOARD_PRESETS.map((preset) => (
              <DropdownMenu.RadioItem
                key={preset}
                value={preset}
                // Bandni bosish panelni o'zi yopmasin: «Ixtiyoriy oraliq»
                // da u ochiq qolishi kerak, qolganini biz yopamiz
                onSelect={(event) => event.preventDefault()}
                className={MENU_ITEM}
              >
                <span className="grid size-4 shrink-0 place-items-center">
                  <DropdownMenu.ItemIndicator>
                    <Check className="size-4 text-[var(--accent-on-soft)]" strokeWidth={3} />
                  </DropdownMenu.ItemIndicator>
                </span>
                {t(PERIOD_LABEL_KEY[preset])}
              </DropdownMenu.RadioItem>
            ))}
          </DropdownMenu.RadioGroup>

          {editing && (
            /*
              `onKeyDown` dagi `stopPropagation` SHART: Radix menyusi Tab
              va harfli tugmalarni o'zi ushlaydi (bandlar orasida yurish
              uchun) — busiz sana maydonlariga klaviatura bilan kirib
              bo'lmasdi (`FilterPopover` dagi bilan bir xil sabab).
            */
            <div
              className="mt-1 space-y-2 border-t border-[var(--border)] px-2.5 pt-3 pb-1"
              onKeyDown={(event) => event.stopPropagation()}
            >
              <TextField
                label={t('common.dateFrom')}
                type="date"
                max={today}
                value={draft.from}
                onChange={(event) => setDraft((previous) => ({ ...previous, from: event.target.value }))}
              />
              <TextField
                label={t('common.dateTo')}
                type="date"
                max={today}
                value={draft.to}
                onChange={(event) => setDraft((previous) => ({ ...previous, to: event.target.value }))}
              />

              {(inverted || tooLong) && (
                <p className="text-caption1 text-ios-red">
                  {errorMessage(inverted ? 'davomat.davr_notogri' : 'davomat.davr_uzun')}
                </p>
              )}

              <div className="flex justify-end gap-1.5 pt-1">
                <Button type="button" size="sm" variant="ghost" onClick={() => setOpen(false)}>
                  {t('common.cancel')}
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="primary"
                  disabled={invalid}
                  onClick={() => {
                    onChange('custom', { from: draft.from, to: draft.to })
                    setOpen(false)
                  }}
                >
                  {t('attendance.statsApply')}
                </Button>
              </div>
            </div>
          )}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

/** Hal qilingan davrning ikki chegarasi — «Ixtiyoriy oraliq» maydonlari uchun. */
function periodBounds(period: DashboardPeriod): { from: string; to: string } {
  return period.kind === 'day'
    ? { from: period.date, to: period.date }
    : { from: period.from, to: period.to }
}

/* ══════════════════════════════════════════════════════ filtr ustuni */

/**
 * O'ngdagi DOIMIY filtr ustuni — Verifix joylashuvi (SPEC C1).
 *
 * Faqat sahifa maydoni keng bo'lganda chiziladi (`@min-[1100px]/page`,
 * fayl boshidagi izoh). Torroq maydonda u umuman
 * render qilinmaydi (`hidden`, «yashirilgan blok» emas): ichida uchta
 * `<select>` va ikkita sana maydoni bor va ular yashirin holatda ham
 * fokus tartibida qolib, klaviatura bilan yurishni buzardi.
 *
 * Ustundagi va ochiluvchi paneldagi filtrlar BITTA manba ustida ishlaydi
 * (manzil) — ikki nusxa holat yo'q, ya'ni ular hech qachon ajralib
 * ketmaydi.
 */
function FilterColumn({
  period,
  today,
  birthdays,
  canView,
  canOpenEmployee,
  entity,
  department,
  position,
  legalEntities,
  departments,
  positions,
  orgCount,
  refreshing,
  onPeriod,
  onOrg,
  onRefresh,
}: {
  period: DashboardPeriod
  today: string
  birthdays: BirthdayFilters
  canView: boolean
  canOpenEmployee: boolean
  entity: string | undefined
  department: string | undefined
  position: string | undefined
  legalEntities: readonly SelectOption[]
  departments: readonly SelectOption[]
  positions: readonly SelectOption[]
  orgCount: number
  refreshing: boolean
  onPeriod: (preset: DashboardPreset, custom?: { from: string; to: string }) => void
  onOrg: (patch: Pick<DashboardSearch, 'entity' | 'department' | 'position'>) => void
  onRefresh: () => void
}) {
  const { t } = useLocale()
  const { isAttendanceScoped } = useAuth()

  return (
    /*
      ⚠️ Ustunning O'ZI suriladi.

      `sticky` — «suriladigan oqim ichida vaqtincha qadalib turish» degani
      (`app-shell.tsx` dagi izoh). Balandligi cheklanmagan qadalgan element
      esa ekrandan uzun bo'lsa, uning PASTKI qismiga umuman yetib
      bo'lmaydi: sahifa surilaveradi, ustun tepada turaveradi va oxirgi
      qatorlar doim ko'rinmas joyda qoladi. Tug'ilgan kunlar kartasi
      qo'shilgach aynan shu holat yuz berdi.

      Chegara — ilova qobig'ining suriladigan maydoni (`main`) balandligi:
      ekran − yuqori panel (64 px + chegara). Qolgan ~15 px ataylab bo'sh
      qoldirilgan, aks holda ustun ekranning eng pastki chekkasiga
      yopishib turardi.

      `overscroll-contain` ATAYLAB yo'q (tabel katagidan farqi): ustun
      oxirigacha surilgach, sichqoncha uning ustida turganda ham sahifa
      surilishda davom etishi kerak.
    */
    <aside className="hidden space-y-4 @min-[1100px]/page:sticky @min-[1100px]/page:top-0 @min-[1100px]/page:block @min-[1100px]/page:max-h-[calc(100dvh-5rem)] @min-[1100px]/page:overflow-y-auto">
      <Card className="space-y-4 p-4">
        <div className="flex items-center gap-2.5">
          <IconTile icon={ListFilter} tone="gray" size="sm" />
          <h2 className="text-subhead font-semibold">{t('common.filters')}</h2>
        </div>

        <div className="space-y-1.5">
          <span className="block text-footnote font-medium text-[var(--fg-secondary)]">
            {t('attendance.statsPeriod')}
          </span>
          <PeriodPicker period={period} today={today} onChange={onPeriod} full />
        </div>

        {!isAttendanceScoped && (
          <>
            <Select
              label={t('employee.legalEntity')}
              placeholder={t('employees.anyLegalEntity')}
              options={legalEntities}
              value={entity ?? ''}
              onChange={(event) =>
                onOrg({ entity: blankToUndefined(event.target.value), department, position })
              }
            />
            <Select
              label={t('employee.department')}
              placeholder={t('employees.anyDepartment')}
              options={departments}
              value={department ?? ''}
              onChange={(event) =>
                onOrg({ entity, department: blankToUndefined(event.target.value), position })
              }
            />
            <Select
              label={t('employee.position')}
              placeholder={t('employees.anyPosition')}
              options={positions}
              value={position ?? ''}
              onChange={(event) =>
                onOrg({ entity, department, position: blankToUndefined(event.target.value) })
              }
            />
          </>
        )}
        <div className="space-y-1.5 pt-1">
          <Button type="button" variant="primary" className="w-full" onClick={onRefresh}>
            <RefreshCw className={cn(refreshing && 'animate-spin')} />
            {t('attendance.statsRefresh')}
          </Button>

          {orgCount > 0 && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="w-full"
              onClick={() => onOrg({ entity: undefined, department: undefined, position: undefined })}
            >
              <X />
              {t('employees.clearFilters')}
            </Button>
          )}
        </div>
      </Card>

      {/*
        Tug'ilgan kunlar — filtrlarning OSTIDA: u filtrga bo'ysunadi
        (bo'lim tanlansa ro'yxat ham torayadi), ya'ni tartib sababni
        natijadan oldin qo'yadi. Faqat keng ekranda: tor ekranda ustun
        umuman chizilmaydi va telefon ko'rinishi o'zgarmaydi (§E5).
      */}
      {/* `/api/dashboard/birthdays` is closed to a «Davomat mas'uli» */}
      {!isAttendanceScoped && (
        <BirthdaysCard filters={birthdays} enabled={canView} canOpenEmployee={canOpenEmployee} />
      )}
    </aside>
  )
}

/* ═══════════════════════════════════════════════════════════ asboblar */

/**
 * Jadval tepasidagi holat chiplari: «Hammasi 81 · Vaqtida 41 · …»
 * (SPEC C4 — Verifix ekranidagi qator, UCHALA ko'rinishda ham).
 *
 * Diagrammaning bo'lagi va legendasi bilan BIR XIL amalni bajaradi
 * (`toggleStatus`), lekin ular `list` ko'rinishida yo'q — chip qatori esa
 * doim turadi. Shu sababli holat filtri endi qaysi ko'rinishda bo'lsa
 * ham bitta bosish masofasida.
 *
 * Sanoqlar `counts` dan: qidiruv va holat filtriga BO'YSUNMAYDI (§8.4),
 * ya'ni «Kechikkanlar» bosilganda qolgan chiplar nolga aylanmaydi.
 */
function StatusChipRow({
  total,
  slices,
  active,
  loading,
  onSelect,
  onClear,
}: {
  total: number
  slices: readonly DashboardSlice[]
  active: DashboardStatus | null
  loading: boolean
  onSelect: (status: DashboardStatus) => void
  onClear: () => void
}) {
  const { t, num } = useLocale()

  if (loading) {
    return (
      <div className="flex flex-wrap gap-1 px-3 pb-3 sm:px-4">
        {Array.from({ length: 7 }, (_, index) => (
          <Skeleton key={index} className="h-7 w-20 rounded-full" />
        ))}
      </div>
    )
  }

  return (
    <div
      role="group"
      aria-label={t('attendance.statsColStatus')}
      className="flex flex-wrap gap-1 px-3 pb-3 sm:px-4"
    >
      <StatusChip
        label={t('common.all')}
        count={num(total)}
        selected={active === null}
        onClick={onClear}
      />

      {slices.map((slice) => {
        const look = DASHBOARD_STATUS_LOOK[slice.status]

        return (
          <StatusChip
            key={slice.status}
            label={t(look.legendKey)}
            count={num(slice.count)}
            dotClass={look.dotClass}
            selected={active === slice.status}
            onClick={() => onSelect(slice.status)}
          />
        )
      })}
    </div>
  )
}

/**
 * Bitta chip. Tanlangani tint bilan, qolganlari sokin kulrang fonda —
 * iOS segmentli boshqaruvi ruhida.
 *
 * ⚠️ ZICH va CHEGARASIZ (egasi 2026-09-18: «juda yirik»). Birinchi
 * versiyada har chip 32 px balandlikda, chegarali va 13 px matnli edi —
 * yettitasi ikki qatorga tushib, jadval ustida legendaning ikkinchi
 * nusxasidek og'ir turardi. Endi 28 px, 12 px matn va chegara o'rniga
 * fon: qator odatda bitta satrga sig'adi va ko'z jadvalga tezroq tushadi.
 * Nom KESILMAYDI («Ish kuni boshlanmag…» — birinchi versiyaning xatosi).
 */
function StatusChip({
  label,
  count,
  dotClass,
  selected,
  onClick,
}: {
  label: string
  count: string
  dotClass?: string
  selected: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onClick}
      className={cn(
        'press flex h-7 items-center gap-1.5 rounded-full px-2.5 text-caption1 font-medium whitespace-nowrap',
        'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
        selected
          ? 'bg-[var(--accent-soft)] text-[var(--accent-on-soft)]'
          : 'bg-[var(--bg-sunken)] text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
      )}
    >
      {dotClass !== undefined && (
        <span aria-hidden className={cn('size-1.5 shrink-0 rounded-full', dotClass)} />
      )}
      <span>{label}</span>
      <span className={cn('tabular font-semibold', !selected && 'text-[var(--fg)]')}>{count}</span>
    </button>
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
 * Rasm va ism: familiya qalin, ism va otasining ismi xira (Verifix'dagi
 * ko'rinish). HR odamni YUZIDAN taniydi — shu sababli haqiqiy rasm
 * (`EmployeePhoto`), bosh harflar faqat rasm yo'q bo'lsa.
 *
 * `inline` — jadvalda BIR QATOR (egasi 2026-10-22): ism familiyaning
 * yonida. Tor ekrandagi kartada esa ikki qator — u yerda en tor.
 */
function EmployeeName({
  item,
  linked,
  size = 'md',
  inline = false,
}: {
  item: DashboardItem
  linked: boolean
  size?: AvatarSize
  inline?: boolean
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
      {inline ? (
        <span>
          <span className={cn('font-semibold', linked && 'text-[var(--accent-text)]')}>{surname}</span>
          {given !== '' && <span className="text-[var(--fg-secondary)]"> {given}</span>}
        </span>
      ) : (
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
      )}
    </>
  )

  if (!linked) {
    return <span className="flex min-w-0 items-center gap-3">{content}</span>
  }

  return (
    <Link
      to="/xodimlar/$employeeId"
      params={{ employeeId: item.employeeId }}
      search={{ tab: 'attendance' }}
      // Qatorning o'zi ham kartani ochadi — havola bosilganda ikki marta o'tilmasin
      onClick={(event) => event.stopPropagation()}
      className="press flex min-w-0 items-center gap-3 rounded-md hover:underline"
    >
      {content}
    </Link>
  )
}

/** «08:03» — Toshkent vaqtida; bo'lmasa «—» (masalan hali ketmagan). */
function TimeCell({ value, auto = false }: { value: string | null; auto?: boolean }) {
  return (
    <span className={cn('tabular text-subhead', value === null && 'text-[var(--fg-tertiary)]')}>
      <AttendanceExitTime value={value} auto={auto} />
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
                    <AttendanceExitTime value={item.leftAt} auto={item.leftAuto} />
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
