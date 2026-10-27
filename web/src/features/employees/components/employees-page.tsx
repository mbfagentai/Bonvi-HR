import { getRouteApi, Link } from '@tanstack/react-router'
import type { SortingState } from '@tanstack/react-table'
import { ChevronRight, SearchX, Timer, Users } from 'lucide-react'
import { useCallback, useEffect, useMemo } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { isDateRangeInverted } from '@/shared/lib/list-search'
import { usePageSize } from '@/shared/lib/use-page-size'
import {
  Alert,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  Pagination,
  buttonVariants,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import type { EmployeeFilterParams, EmployeeListItem } from '../api/employees-api'
import { useEmployees, useExportEmployees, useFilterOptions } from '../api/employees-queries'
import { employeeErrorCode, isRateLimited } from '../lib/employee-error'
import {
  EMPLOYEES_CREATE,
  EMPLOYMENT_HIRE,
  REPORTS_EXPORT,
} from '../lib/employee-presentation'
import {
  DEFAULT_SORT,
  filtersToSearch,
  isFiltered,
  normalizeSort,
  searchToFilters,
  type EmployeesFilterState,
  type EmployeesSearch,
  type EmployeesSortParam,
  type EmployeesView,
} from '../lib/employees-search'
import { DirectionBadge, FlagBadge, NeedsReviewMark, StatusBadge } from './employee-badges'
import { EmployeePhoto } from './employee-photo'
import { EmployeesGrid } from './employees-grid'
import { EmployeesToolbar } from './employees-toolbar'

const helper = dataColumnHelper<EmployeeListItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: EmployeeListItem[] = []

/**
 * Marshrut «apisi» — `Route` obyektini import qilmasdan uning
 * qidiruv parametrlariga va navigatsiyasiga tiplashgan kirish.
 *
 * NEGA to'g'ridan-to'g'ri `Route` emas: marshrut fayli SHU komponentni
 * import qiladi, teskari import esa aylanma bog'liqlik hosil qilardi.
 */
const route = getRouteApi('/_app/xodimlar/')

/**
 * Xodimlar ro'yxati — tizimning asosiy ekrani.
 *
 * Ikki ko'rinish bitta ma'lumot ustida: jadval (solishtirish, saralash) va
 * kartochka (rasm bo'yicha tanish). Ikkalasi ham SERVER TOMONDAN
 * saralangan va sahifalangan ma'lumotni ko'rsatadi — 540 xodimni
 * brauzerga to'kib, keyin uni klientda kesish HR ning eski Excel
 * muammosini takrorlash bo'lardi.
 */
export function EmployeesPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  /*
   * Ruxsat SAQLANDI, tugma esa yo'q: bo'sh ro'yxatdagi «Sinov muddati»
   * yo'nalmasi faqat ishga qabul qila oladigan odamga ko'rsatiladi.
   * Ko'ra olmaydigan odamni u yerga yuborish — yopiq eshikka boshlash.
   */
  const canHire = hasPermission(EMPLOYEES_CREATE) && hasPermission(EMPLOYMENT_HIRE)
  const canExport = hasPermission(REPORTS_EXPORT)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const page = search.page ?? 1
  const view = search.view ?? 'table'
  const sortParam = search.sort ?? DEFAULT_SORT

  // «Barchasi» faqat jadvalda: kartochkalar virtual chizilmaydi
  const { pageSize, setPageSize, allowAll } = usePageSize('employees', view === 'table')

  const filters = useMemo(() => searchToFilters(search), [search])
  const sorting = useMemo(() => sortingOf(sortParam), [sortParam])

  const options = useFilterOptions()

  /**
   * So'rov filtri — bir marta yig'iladi, IKKI joyda ishlatiladi:
   * ekrandagi ro'yxat ham, Excel eksporti ham aynan shundan quriladi.
   *
   * NEGA shunday: «ekranda nima ko'rsang, faylda ham shu» talabini kod
   * darajasida kafolatlaydigan yagona usul — ikkinchi nusxa yozmaslik.
   * Aynan shu sababdan saralash ham (`sort`) shu obyektda turibdi: u
   * toolbar'da emas, manzilda yashaydi va eksport uni albatta olib
   * ketishi kerak — aks holda fayl har doim familiya bo'yicha, ekran esa
   * masalan sana bo'yicha saralangan bo'lardi.
   *
   * Sahifa raqami ATAYLAB kirmaydi: eksport sahifalanmaydi (butun mos
   * ro'yxatni beradi), ro'yxat esa uni quyida o'zi qo'shadi.
   */
  const listFilters = useMemo<EmployeeFilterParams>(
    () => ({
      search: filters.search,
      departmentId: filters.departmentId,
      positionId: filters.positionId,
      directionId: filters.directionId,
      legalEntityId: filters.legalEntityId,
      status: filters.status === '' ? undefined : filters.status,
      needsReview: filters.needsReview,
      sort: sortParam,
      // Sana oralig'i ham shu obyektda — ya'ni Excel fayliga ham O'ZI tushadi
      from: filters.from,
      to: filters.to,
    }),
    [filters, sortParam],
  )

  /*
    Teskari oraliq bilan na ro'yxat, na eksport so'rovi ketadi: server
    baribir 400 qaytaradi, ekran esa sababni toolbar ostida darhol aytadi
    (`attendance-passes-page.tsx` naqshi).
  */
  const rangeInvalid = isDateRangeInverted(filters)

  const query = useEmployees({ ...listFilters, page, pageSize }, !rangeInvalid)

  /**
   * Eksport holati.
   *
   * Xato mutatsiyaning o'zida saqlanadi va qo'lda tozalanmaydi: keyingi
   * `mutate` chaqiruvi holatni «kutilmoqda» ga o'tkazib, eski xatoni
   * o'zi yo'q qiladi. Ya'ni HR filtrni toraytirib qayta bosganda
   * ogohlantirish qolib ketmaydi, muvaffaqiyatsizlikda esa ekranda
   * turaveradi — chaqnab o'tib ketmaydi.
   */
  const exportList = useExportEmployees()

  /**
   * Holatni manzil satriga yozadi.
   *
   * NEGA standart holatda `replace`: filtr, qidiruv, saralash va
   * ko'rinish — bitta ekran ichidagi aniqlashtirish, alohida «sahifa»
   * emas. Har biri tarixga yozilsa, HR ro'yxatdan chiqish uchun «orqaga»
   * tugmasini o'nlab marta bosishga majbur bo'lardi. Sahifa raqami esa
   * aksincha — haqiqiy qadam, u tarixga yoziladi.
   */
  const setSearch = useCallback(
    (
      next: (previous: EmployeesSearch) => EmployeesSearch,
      history: 'push' | 'replace' = 'replace',
    ) => {
      void navigate({ to: '/xodimlar', search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  /** Filtr o'zgarganda 7-sahifada qolib ketmaslik kerak — u endi bo'sh. */
  const changeFilters = useCallback(
    (next: EmployeesFilterState) => {
      setSearch((previous) => ({ ...previous, ...filtersToSearch(next), page: undefined }))
    },
    [setSearch],
  )

  /** Saralash o'zgarsa ham xuddi shunday: yozuvlar boshqa sahifalarga ko'chadi. */
  const changeSorting = useCallback(
    (next: SortingState) => {
      setSearch((previous) => ({ ...previous, sort: sortParamOf(next), page: undefined }))
    },
    [setSearch],
  )

  const changeView = useCallback(
    (next: EmployeesView) => {
      // 'table' — standart, u manzilni ifloslantirmasin
      setSearch((previous) => ({ ...previous, view: next === 'table' ? undefined : next }))
    },
    [setSearch],
  )

  const changePage = useCallback(
    (next: number) => {
      setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push')
    },
    [setSearch],
  )

  /** Hajm o'zgarsa birinchi sahifadan — eski raqam yangi hajmda boshqa odamlar. */
  const changePageSize = useCallback(
    (next: number) => {
      setPageSize(next)
      setSearch((previous) => ({ ...previous, page: undefined }))
    },
    [setPageSize, setSearch],
  )

  const openEmployee = useCallback(
    (employee: EmployeeListItem) => {
      void navigate({ to: '/xodimlar/$employeeId', params: { employeeId: employee.id } })
    },
    [navigate],
  )

  /**
   * Ro'yxat qisqarganda oxirgi sahifadan narida qolib ketmaslik.
   *
   * NEGA kerak: filtr o'zgarganda sahifa 1 ga tushadi, lekin chegaradan
   * oshib qolishning boshqa yo'llari ham bor — xodim bo'shatilgach ro'yxat
   * qisqaradi, eski havolada `?page=7` turadi yoki manzilga qo'lda raqam
   * yoziladi. U holda server bo'sh ro'yxat qaytaradi va HR yozuvlar bor
   * bo'la turib BO'SH EKRAN ko'radi. `Pagination` bu yerda yordam
   * berolmaydi: u raqamni faqat CHIZISH uchun qisadi.
   *
   * `isPlaceholderData` sharti muhim: sahifa almashayotganda ekranda
   * oldingi so'rov javobi turadi va uning `total` i eski — unga qarab
   * sakrash HR ni noto'g'ri sahifaga olib borardi.
   *
   * `replace` — noto'g'ri sahifa tarixda qolmaydi, aks holda «orqaga»
   * o'sha bo'sh sahifaga qaytarardi.
   */
  useEffect(() => {
    if (query.data === undefined || query.isPlaceholderData) return

    const lastPage = Math.max(1, Math.ceil(query.data.total / pageSize))
    if (page <= lastPage) return

    setSearch((previous) => ({ ...previous, page: lastPage <= 1 ? undefined : lastPage }))
  }, [query.data, query.isPlaceholderData, page, pageSize, setSearch])

  /*
    Ro'yxat bir nechta holatni aralash ko'rsatadimi. «Ishdan ketganlar» yoki
    faqat «Sinovda» filtrida holat yorlig'i har qatorda bir xil — ortiqcha
    (egasi 2026-10-22: «ortiqcha ma'lumot»), u faqat aralash ro'yxatda kerak.
  */
  const mixedStatus = filters.status === 'employed' || filters.status === ''

  const columns = useMemo<Array<DataColumn<EmployeeListItem>>>(
    () => [
      helper.accessor('fullName', {
        header: t('employee.fullName'),
        /*
          ⭐ BIR QATOR (egasi 2026-10-22): rasm, to'liq ism, «tekshirish kerak»
          belgisi va holat yorlig'i bir chiziqda. Ilgari ism ikki qatorga,
          holat va yo'nalishlar yana alohida qatorlarga tushib, bitta odam
          ikki odamlik joyni egallardi. Ism kesilmaydi — ustun eng uzun
          ismgacha kengayadi (`DataTable` — `whitespace-nowrap`), yo'nalishlar
          esa o'z ustunida.
        */
        cell: ({ row }) => {
          const employee = row.original

          return (
            <div className="flex items-center gap-3">
              <EmployeePhoto
                employeeId={employee.id}
                photoKey={employee.photoKey}
                name={employee.fullName}
                size="md"
              />

              <span className="font-medium">{employee.fullName}</span>
              {employee.needsReview && <NeedsReviewMark compact />}

              {/*
                Holat yorlig'i — FAQAT doimiy bo'lmaganda va FAQAT aralash
                ro'yxatda: sariq — sinovda, kulrang — ishdan ketgan.
                «Ishlayapti» qatorlarning ko'pchiligida takrorlanib, aynan
                ajratib turishi kerak bo'lgan sarig'ini ko'mib yuborardi.
              */}
              {mixedStatus && employee.status !== 'active' && (
                <StatusBadge status={employee.status} size="sm" />
              )}
            </div>
          )
        },
      }),

      helper.accessor('positionName', {
        header: t('employee.position'),
        enableSorting: false,
        // Juda uzun lavozim «…» bilan, to'liq nomi `title` da (`DataTable` — `maxWidth`)
        meta: { maxWidth: '260px' },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('departmentName', {
        header: t('employee.department'),
        enableSorting: false,
        meta: { maxWidth: '260px', hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      /*
        Yo'nalishlar — o'z ustunida, bir qatorda. Ko'pi bilan ikkitasi
        yorliq, qolgani «+N» (to'liq ro'yxat `title` da — sichqoncha ustida) —
        5 ta yo'nalishli odam ustunni butun jadvaldan keng qilib yubormasin.
      */
      helper.display({
        id: 'directions',
        header: t('employee.direction'),
        enableSorting: false,
        meta: { hideOnMobile: true },
        cell: ({ row }) => {
          const directions = row.original.directions
          if (directions.length === 0) return <span className="text-[var(--fg-tertiary)]">—</span>

          return (
            <span
              className="flex items-center gap-1"
              title={directions.map((direction) => direction.name).join(', ')}
            >
              {directions.slice(0, 2).map((direction) => (
                <DirectionBadge key={direction.id} name={direction.name} />
              ))}
              {directions.length > 2 && (
                <span className="text-caption2 text-[var(--fg-tertiary)]">+{directions.length - 2}</span>
              )}
            </span>
          )
        },
      }),

      helper.accessor('legalEntityName', {
        header: t('employee.legalEntity'),
        enableSorting: false,
        meta: { hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('hireDate', {
        /*
          Sarlavha shu yerda qisqa: «Ishga kirgan sana» katakni o'zining
          qiymatidan («15.01.1991») ikki barobar keng qilib yuborardi —
          ustun sarlavhadan tor bo'la olmaydi. Qolgan joyda to'liq nom
          ishlatiladi, bu yerda ustun tepasi kontekstning o'zi.
        */
        header: t('employee.hireDateShort'),
        meta: { width: '116px', hideOnMobile: true },
        cell: ({ getValue }) => <DateCell value={getValue()} />,
      }),

      /*
       * Holat yorlig'i ATAYLAB yo'q, belgilar esa qoldi.
       *
       * NEGA: ro'yxat standart holda faqat doimiy xodimlarni ko'rsatadi,
       * ya'ni «Ishlayapti» yorlig'i har qatorda takrorlanadigan va hech
       * nimani ajratmaydigan shovqin bo'lardi. Ustunning o'zi esa kerak:
       * «Rezerv», «Qora ro'yxat», «Nazoratda» kabi belgilar aynan
       * ro'yxatdan ko'zga tashlanishi shart.
       *
       * Belgisi yo'q katak bo'sh qoladi — chiziqcha qo'yilmaydi. Chiziqcha
       * «ma'lumot to'ldirilmagan» degan ma'noni berardi, belgi esa
       * «bor/yo'q» xususiyati: uning yo'qligi normal hol.
       */
      helper.display({
        id: 'flags',
        header: t('employee.flags'),
        enableSorting: false,
        // Kenglik mazmundan: ko'p qatorda belgi yo'q — bo'sh ustun joy yemasin
        cell: ({ row }) => (
          <span className="flex items-center gap-1.5">
            {row.original.activeFlags.map((flag) => (
              <FlagBadge key={flag} type={flag} size="sm" />
            ))}
          </span>
        ),
      }),

      helper.display({
        id: 'open',
        header: '',
        enableSorting: false,
        meta: { align: 'end', width: '40px' },
        cell: () => (
          // iOS ro'yxatidagi «ochiladi» belgisi
          <ChevronRight
            className="ml-auto size-4 text-[var(--fg-tertiary)]"
            strokeWidth={2.5}
          />
        ),
      }),
    ],
    [mixedStatus, t],
  )

  // Teskari oraliqda eski sahifa ko'rinib qolmasin — u boshqa filtrniki
  const employees = rangeInvalid ? EMPTY : (query.data?.items ?? EMPTY)
  const total = rangeInvalid ? 0 : (query.data?.total ?? 0)

  /**
   * `isPending` — hech qachon ma'lumot bo'lmagan, `isFetching` — yangilanmoqda.
   * Sahifadan sahifaga o'tishda eski ro'yxat ko'rinib turadi (skelet emas),
   * shu sababli faqat birinchi yuklashda skelet chiziladi.
   */
  const firstLoad = query.isPending && !rangeInvalid

  const filtered = isFiltered(search)
  const listEmpty = employees.length === 0 && !firstLoad

  /*
    ⭐ «Hammasi sinovda» degan alohida bo'sh ekran OLIB TASHLANDI
    (2026-09-16). U ro'yxat faqat DOIMIY xodimlarni ko'rsatgan davrdan
    qolgan edi: ETL har kimga sinov muddati ochgani uchun ro'yxat bo'sh
    chiqardi va ekran «odamlar bor, ular /sinov sahifasida» deb yo'l
    ko'rsatardi.

    Endi standart filtr sinovdagilarni ham qamraydi (`DEFAULT_STATUS =
    'employed'`), ya'ni bunday holat umuman yuzaga kelmaydi — bo'sh
    ro'yxat endi HAQIQATAN «bazada ishlayotgan odam yo'q» degani.
    Matnni qoldirish yolg'on yo'l ko'rsatgan bo'lardi.
  */
  function emptyState() {
    if (filtered) {
      return (
        <EmptyState
          icon={SearchX}
          title={t('employees.notFoundTitle')}
          description={t('employees.notFoundText')}
        />
      )
    }

    /*
      Bazada umuman odam yo'q. Ilgari bu yerda «Ishga qabul qilish» tugmasi
      turardi va u oynani SHU YERDA ochardi — endi u ham «Sinov muddati»
      sahifasiga yo'naltiradi, chunki forma o'sha yerga ko'chdi. Yo'nalish
      yuqoridagi tarmoq bilan bir xil: /xodimlar dan xodim YARATILMAYDI.
    */
    return (
      <EmptyState
        icon={Users}
        title={t('employees.emptyTitle')}
        description={t('employees.emptyHireHint')}
        action={
          canHire ? (
            <Link to="/sinov" className={buttonVariants({ variant: 'primary' })}>
              <Timer />
              {t('nav.probation')}
            </Link>
          ) : undefined
        }
      />
    )
  }

  // Teskari oraliqda bo'sh holat chizilmaydi — sabab ogohlantirishda
  const empty = listEmpty && !rangeInvalid ? emptyState() : null

  return (
    <div data-list-frame className="space-y-6 frame:flex frame:h-full frame:flex-col">
      {/*
        «Ishga qabul» tugmasi bu sahifada ATAYLAB yo'q — u `/sinov` ga
        ko'chdi (`probations-page.tsx`). Sabab oqimda: bu ro'yxat DOIMIY
        xodimlarniki (standart filtri `status=active`), yangi odam esa har
        doim sinovdan boshlaydi va avval o'sha sahifada paydo bo'ladi.
        Xodim yaratish tugmasi doimiylar ro'yxatida turgani oqimni teskari
        ko'rsatardi: go'yo odam avval doimiy bo'lib, keyin qandaydir yo'l
        bilan sinovga tushadi.

        ⚠️ `HireDialog` ning O'ZI shu modulda qoldi: u shaxs, mehnat
        munosabati va ma'lumotnomalar bilan ishlaydi — sinov esa uning
        NATIJASI, sababi emas.
      */}
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={Users} tone="brand" size="lg" />
            {t('nav.employees')}
          </span>
        }
        subtitle={
          firstLoad
            ? t('common.loading')
            : t('employees.subtitle', { n: num(total) })
        }
      />

      {query.isError && (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {errorMessage(employeeErrorCode(query.error))}
        </Alert>
      )}

      <Card className="overflow-hidden frame:flex frame:flex-1 frame:flex-col">
        <EmployeesToolbar
          filters={filters}
          onChange={changeFilters}
          view={view}
          onViewChange={changeView}
          options={options}
          onExport={canExport ? () => exportList.mutate(listFilters) : undefined}
          exporting={exportList.isPending}
          exportDisabled={rangeInvalid}
        />

        {rangeInvalid && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="warning">{errorMessage('royxat.sana_oraligi_notogri')}</Alert>
          </div>
        )}

        {/*
          Eksport xatosi AYNAN shu yerda — tugmaning ostida, kartaning
          ichida. Sahifa tepasidagi ogohlantirish (`loadFailed`) yonida
          tursa, HR uni tugmadan uzoqda ko'rardi va nima bilan bog'liqligi
          tushunarsiz bo'lardi; oynada ko'rsatilsa esa yopilgach yo'qolib,
          «filtrni toraytiring» maslahati bilan birga ketardi.

          429 alohida gapiriladi: uning yagona yechimi — biroz kutish,
          filtrni o'zgartirish esa yordam bermaydi.
        */}
        {exportList.isError && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="danger" title={t('employees.exportFailed')}>
              {isRateLimited(exportList.error)
                ? t('employees.exportTooOften')
                : errorMessage(employeeErrorCode(exportList.error))}
            </Alert>
          </div>
        )}

        {view === 'table' ? (
          <DataTable
            columns={columns}
            data={employees}
            getRowId={(employee) => employee.id}
            loading={firstLoad}
            skeletonRows={8}
            sorting={sorting}
            onSortingChange={changeSorting}
            onRowClick={openEmployee}
            rowLabel={(employee) => employee.fullName}
            empty={empty}
            fill
          />
        ) : (
          // Kartochkalar ham kartaning ichida aylanadi — sahifalash joyida qoladi
          <div data-list-scroll className="frame:min-h-40 frame:flex-1 frame:overflow-y-auto frame:[contain:size]">
            <EmployeesGrid
              employees={employees}
              loading={firstLoad}
              onSelect={openEmployee}
            />
            {empty}
          </div>
        )}

        {total > 0 && (
          <Pagination
            page={page}
            pageSize={pageSize}
            total={total}
            onPageChange={changePage}
            onPageSizeChange={changePageSize}
            allowAll={allowAll}
            disabled={query.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>
    </div>
  )
}

/**
 * Manzildagi saralash → jadval modeli.
 *
 * Ikki tomonlama o'girish shu yerda turibdi, chunki `SortingState` —
 * jadval kutubxonasining tipi, manzil esa u haqda hech narsa bilmasligi
 * kerak.
 */
function sortingOf(param: EmployeesSortParam): SortingState {
  return param.startsWith('-')
    ? [{ id: param.slice(1), desc: true }]
    : [{ id: param, desc: false }]
}

/**
 * Jadval modeli → manzil.
 *
 * Saralash olib tashlansa ham, standart ustun tanlansa ham `undefined`
 * bo'ladi — ya'ni manzilda saralash umuman ko'rinmaydi va ro'yxat
 * standart tartibda chiziladi.
 */
function sortParamOf(sorting: SortingState): EmployeesSortParam | undefined {
  const first = sorting[0]
  if (first === undefined) return undefined

  return normalizeSort(first.desc ? `-${first.id}` : first.id)
}

/** Bo'sh katak chiziqcha bilan — «yuklanmadimi?» degan savol tug'ilmasin. */
function TextCell({ value }: { value: string | null }) {
  if (value === null || value === '') {
    return <span className="text-[var(--fg-tertiary)]">—</span>
  }
  return <span className="text-[var(--fg-secondary)]">{value}</span>
}

function DateCell({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null || value === '') {
    return <span className="text-[var(--fg-tertiary)]">—</span>
  }
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
