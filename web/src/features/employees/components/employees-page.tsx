import { getRouteApi, Link } from '@tanstack/react-router'
import type { SortingState } from '@tanstack/react-table'
import { ChevronRight, SearchX, Timer, UserPlus, Users } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Button,
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
import type { EmployeeListItem } from '../api/employees-api'
import { useEmployees, useFilterOptions } from '../api/employees-queries'
import { employeeErrorCode } from '../lib/employee-error'
import { EMPLOYEES_CREATE, EMPLOYMENT_MANAGE } from '../lib/employee-presentation'
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
import { DirectionBadge, FlagBadge, NeedsReviewMark } from './employee-badges'
import { EmployeePhoto } from './employee-photo'
import { EmployeesGrid } from './employees-grid'
import { EmployeesToolbar } from './employees-toolbar'
import { HireDialog } from './hire-dialog'

const helper = dataColumnHelper<EmployeeListItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: EmployeeListItem[] = []

/**
 * Bir sahifadagi yozuvlar soni.
 *
 * 50 — foydalanuvchi so'ragan miqdor: HR bir ekranda imkon qadar ko'p
 * odamni ko'rishni xohlaydi va «keyingi» tugmasini kamroq bosadi.
 * Serverning yuqori chegarasi 200, ya'ni 50 bemalol o'tadi; kartochka
 * to'rida ham 50 — 2, 3 va 4 ustunda to'liq qatorlar bo'ladi.
 */
const PAGE_SIZE = 50

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

  const canHire = hasPermission(EMPLOYEES_CREATE) && hasPermission(EMPLOYMENT_MANAGE)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const [hireOpen, setHireOpen] = useState(false)

  const page = search.page ?? 1
  const view = search.view ?? 'table'
  const sortParam = search.sort ?? DEFAULT_SORT

  const filters = useMemo(() => searchToFilters(search), [search])
  const sorting = useMemo(() => sortingOf(sortParam), [sortParam])

  const options = useFilterOptions()

  const query = useEmployees({
    search: filters.search,
    departmentId: filters.departmentId,
    positionId: filters.positionId,
    directionId: filters.directionId,
    legalEntityId: filters.legalEntityId,
    status: filters.status === '' ? undefined : filters.status,
    needsReview: filters.needsReview,
    page,
    pageSize: PAGE_SIZE,
    sort: sortParam,
  })

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

    const lastPage = Math.max(1, Math.ceil(query.data.total / PAGE_SIZE))
    if (page <= lastPage) return

    setSearch((previous) => ({ ...previous, page: lastPage <= 1 ? undefined : lastPage }))
  }, [query.data, query.isPlaceholderData, page, setSearch])

  const columns = useMemo<Array<DataColumn<EmployeeListItem>>>(
    () => [
      helper.accessor('fullName', {
        header: t('employee.fullName'),
        /*
          Eng kichik kenglik ATAYLAB qo'yilgan. Qolgan ustunlar aniq
          o'lchamda, F.I.Sh. esa ortib qolganini oladi — tor ekranda u
          birinchi bo'lib qisiladi va ism «Абдуқодиро…» bo'lib kesiladi.
          O'zbekcha to'liq ism uzun («Абдуқодиров Шерзод Нурмат ўғли»),
          HR esa odamni aynan ismidan taniydi.
        */
        meta: { width: '340px' },
        cell: ({ row }) => {
          const employee = row.original

          return (
            <div className="flex min-w-0 items-center gap-3">
              <EmployeePhoto
                employeeId={employee.id}
                photoKey={employee.photoKey}
                name={employee.fullName}
                size="md"
              />

              <div className="min-w-0">
                <p className="truncate-2 flex items-center gap-1.5 font-medium">
                  {employee.fullName}
                  {employee.needsReview && <NeedsReviewMark compact />}
                </p>

                {employee.directions.length > 0 && (
                  <span className="mt-1 flex flex-wrap items-center gap-1">
                    {employee.directions.slice(0, 2).map((direction) => (
                      <DirectionBadge key={direction.id} name={direction.name} />
                    ))}
                    {employee.directions.length > 2 && (
                      <span className="text-caption2 text-[var(--fg-tertiary)]">
                        +{employee.directions.length - 2}
                      </span>
                    )}
                  </span>
                )}
              </div>
            </div>
          )
        },
      }),

      helper.accessor('positionName', {
        header: t('employee.position'),
        enableSorting: false,
        meta: { width: '180px' },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('departmentName', {
        header: t('employee.department'),
        enableSorting: false,
        meta: { width: '180px', hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('legalEntityName', {
        header: t('employee.legalEntity'),
        enableSorting: false,
        meta: { width: '150px', hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('hireDate', {
        header: t('employee.hireDate'),
        meta: { width: '124px', hideOnMobile: true },
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
        meta: { width: '140px' },
        cell: ({ row }) => (
          <span className="flex flex-wrap items-center gap-1.5">
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
        meta: { align: 'end', width: '56px' },
        cell: () => (
          // iOS ro'yxatidagi «ochiladi» belgisi
          <ChevronRight
            className="ml-auto size-4 text-[var(--fg-tertiary)]"
            strokeWidth={2.5}
          />
        ),
      }),
    ],
    [t],
  )

  const employees = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0

  /**
   * `isPending` — hech qachon ma'lumot bo'lmagan, `isFetching` — yangilanmoqda.
   * Sahifadan sahifaga o'tishda eski ro'yxat ko'rinib turadi (skelet emas),
   * shu sababli faqat birinchi yuklashda skelet chiziladi.
   */
  const firstLoad = query.isPending

  const filtered = isFiltered(search)
  const listEmpty = employees.length === 0 && !firstLoad

  /**
   * Sinovdagilar soni — FAQAT ro'yxat filtrsiz bo'sh chiqqanda so'raladi.
   *
   * NEGA umuman kerak: ro'yxatda doimiy xodimlar turadi, ETL esa har
   * kimga sinov muddati ochgan va hech biri yakunlanmagan — HR bo'sh
   * ekranni ko'rib «ma'lumot yo'qolibdi» deb o'ylashi mumkin. Son bilan
   * gap boshqacha eshitiladi: odamlar bor, ular boshqa sahifada kutyapti.
   *
   * `pageSize: 1` — bizga faqat `total` kerak, yozuvlar emas.
   */
  const probation = useEmployees(
    { status: 'probation', page: 1, pageSize: 1 },
    listEmpty && !filtered,
  )
  const probationCount = probation.data?.total ?? 0

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

    // Sinovdagilar soni hali kelmagan: noto'g'ri matn chaqnab o'tmasin
    if (probation.isFetching) return null

    // Bazada odam bor, lekin hammasi hali sinovda — yo'lni ko'rsatamiz
    if (probationCount > 0) {
      return (
        <EmptyState
          icon={Timer}
          title={t('employees.noActiveTitle')}
          description={t('employees.noActiveText', { n: num(probationCount) })}
          action={
            <Link to="/sinov" className={buttonVariants({ variant: 'primary' })}>
              <Timer />
              {t('nav.probation')}
            </Link>
          }
        />
      )
    }

    return (
      <EmptyState
        icon={Users}
        title={t('employees.emptyTitle')}
        description={t('employees.emptyText')}
        action={
          canHire ? (
            <Button variant="create" onClick={() => setHireOpen(true)}>
              <UserPlus />
              {t('employees.hire')}
            </Button>
          ) : undefined
        }
      />
    )
  }

  const empty = listEmpty ? emptyState() : null

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={Users} tone="brand" size="lg" />
            {t('nav.employees')}
          </span>
        }
        subtitle={
          query.isPending
            ? t('common.loading')
            : t('employees.subtitle', { n: num(total) })
        }
        actions={
          canHire ? (
            <Button variant="create" onClick={() => setHireOpen(true)}>
              <UserPlus />
              {t('employees.hire')}
            </Button>
          ) : undefined
        }
      />

      {query.isError && (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {errorMessage(employeeErrorCode(query.error))}
        </Alert>
      )}

      <Card className="overflow-hidden">
        <EmployeesToolbar
          filters={filters}
          onChange={changeFilters}
          view={view}
          onViewChange={changeView}
          options={options}
        />

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
          />
        ) : (
          <>
            <EmployeesGrid
              employees={employees}
              loading={firstLoad}
              onSelect={openEmployee}
            />
            {empty}
          </>
        )}

        {total > 0 && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            total={total}
            onPageChange={changePage}
            disabled={query.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>

      <HireDialog open={hireOpen} onClose={() => setHireOpen(false)} options={options} />
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
  return <span className="truncate-1 text-[var(--fg-secondary)]">{value}</span>
}

function DateCell({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null || value === '') {
    return <span className="text-[var(--fg-tertiary)]">—</span>
  }
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
