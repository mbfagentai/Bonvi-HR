import { getRouteApi } from '@tanstack/react-router'
import type { SortingState } from '@tanstack/react-table'
import { Award, SearchX } from 'lucide-react'
import { useCallback, useMemo } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { employeeErrorCode, isRateLimited } from '@/features/employees/lib/employee-error'
import {
  INCENTIVES_VIEW,
  INCENTIVE_KIND_LOOK,
  REPORTS_EXPORT,
} from '@/features/employees/lib/employee-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { useClampPage, usePageSize } from '@/shared/lib/use-page-size'
import {
  Alert,
  Badge,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  Pagination,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import type { IncentiveListItem } from '../api/incentives-list-api'
import {
  useExportIncentives,
  useFilterOptions,
  useIncentiveList,
} from '../api/incentives-list-queries'
import {
  DEFAULT_SORT,
  filtersToSearch,
  isFiltered,
  normalizeSort,
  searchToFilters,
  type IncentivesFilterState,
  type IncentivesSearch,
  type IncentivesSortParam,
} from '../lib/incentives-search'
import { IncentivesToolbar } from './incentives-toolbar'

const helper = dataColumnHelper<IncentiveListItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: IncentiveListItem[] = []

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/ragbat')

/**
 * Rag'bat va jarima — barcha xodimlar bo'yicha umumiy ro'yxat.
 *
 * Excel'da bu «91.14 Тақдирланган ходимлар» varag'i edi va u faqat
 * rag'batni bilardi — jarima umuman yozilmasdi, HR uni og'zaki eslab
 * qolardi. Bu yerda ikkalasi bitta jadval va uning filtrlangan
 * ko'rinishlari.
 *
 * ⚠️ HAR BIR YOZUV ALOHIDA QATOR — bitta xodim ro'yxatda bir necha marta
 * ko'rinadi. Bu foydalanuvchi bilan kelishilgan: xodim bo'yicha yig'ma
 * ko'rinish («nechta rag'bat, nechta jarima») ATAYLAB qilinmadi, chunki
 * u «qachon, kimga, nima» degan asosiy savolni yo'qotardi.
 *
 * Xodim kartasidagi «Rag'bat/Jarima» tabidan farqi: u yerda bitta odam
 * va yozuv qo'shish/tahrirlash bor, bu yerda esa hammasi aralash, faqat
 * O'QISH uchun — yozuvni o'zgartirish egasining kartasida bajariladi.
 */
export function IncentivesPage() {
  const { t, num, date, money, percent } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canView = hasPermission(INCENTIVES_VIEW)
  // Server ikkalasini talab qiladi: ko'rish VA fayl olib chiqish.
  const canExport = canView && hasPermission(REPORTS_EXPORT)
  const exportList = useExportIncentives()

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const page = search.page ?? 1
  const { pageSize, setPageSize, allowAll } = usePageSize('incentives', true)
  const sortParam = search.sort ?? DEFAULT_SORT

  const filters = useMemo(() => searchToFilters(search), [search])
  const sorting = useMemo(() => sortingOf(sortParam), [sortParam])
  const options = useFilterOptions()

  /*
   * `canView` yo'q bo'lsa ham so'rov chaqiriladi — `incidents-page.tsx`
   * va `users-page.tsx` dagi bilan bir xil naqsh: server baribir 403
   * qaytaradi (himoya SHU YERDA emas), bu yerda faqat natija
   * ko'rsatilmaydi (pastdagi `!canView` bo'limi). Hook'ni shartli qilish
   * React qoidasini buzardi, `enabled` esa ruxsat tekshiruvini ikki
   * joyga bo'lib yuborardi.
   */
  /*
   * Ro'yxat va Excel eksporti BITTA filtr obyektidan: yangi filtr shu yerga
   * qo'shilsa, u ikkalasiga birga tushadi va «ekranda boshqa, faylda
   * boshqa» holati kelib chiqmaydi (`employees-page.tsx` dagi naqsh).
   */
  const listFilters = useMemo(
    () => ({
      search: filters.search,
      kind: filters.kind === '' ? undefined : filters.kind,
      from: filters.from === '' ? undefined : filters.from,
      to: filters.to === '' ? undefined : filters.to,
      departmentId: filters.departmentId === '' ? undefined : filters.departmentId,
      legalEntityId: filters.legalEntityId === '' ? undefined : filters.legalEntityId,
      sort: sortParam,
    }),
    [filters, sortParam],
  )

  const query = useIncentiveList({ ...listFilters, page, pageSize })

  /** Standart holatda `replace` — filtr aniqlashtirish, alohida qadam emas. */
  const setSearch = useCallback(
    (
      next: (previous: IncentivesSearch) => IncentivesSearch,
      history: 'push' | 'replace' = 'replace',
    ) => {
      void navigate({ to: '/ragbat', search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  /** Filtr o'zgarganda 7-sahifada qolib ketmaslik kerak — u endi bo'sh. */
  const changeFilters = useCallback(
    (next: IncentivesFilterState) => {
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

  const changePage = useCallback(
    (next: number) => {
      setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push')
    },
    [setSearch],
  )

  /** Hajm o'zgarsa birinchi sahifadan — eski raqam yangi hajmda boshqa yozuvlar. */
  const changePageSize = useCallback(
    (next: number) => {
      setPageSize(next)
      setSearch((previous) => ({ ...previous, page: undefined }))
    },
    [setPageSize, setSearch],
  )

  // Eski sahifa raqami yangi hajmda bo'sh bo'lsa — oxirgi sahifaga (`useClampPage`)
  const clampPage = useCallback(
    (last: number) =>
      setSearch((previous) => ({ ...previous, page: last <= 1 ? undefined : last })),
    [setSearch],
  )
  useClampPage(page, pageSize, query.isPlaceholderData ? undefined : query.data?.total, clampPage)

  /**
   * Qator bosilsa — EGASINING kartasi ochiladi.
   *
   * NEGA yozuvning o'zi emas: rag'bat/jarima alohida sahifaga arzimaydi,
   * unda ro'yxatda ko'rinmaydigan atigi ikki maydon bor (sabab va
   * sovg'a). Xodim kartasi esa savolning davomiga javob beradi — «bu
   * odamga yana nima berilgan, umuman kim u».
   */
  const openEmployee = useCallback(
    (row: IncentiveListItem) => {
      void navigate({ to: '/xodimlar/$employeeId', params: { employeeId: row.employeeId } })
    },
    [navigate],
  )

  const columns = useMemo<Array<DataColumn<IncentiveListItem>>>(
    () => [
      helper.accessor('date', {
        /*
          130px — boshqa sana ustunlari bilan bir xil (`employments-page.tsx`,
          `employee-incentives-tab.tsx`): «26.08.2026» (10 belgi, tabular)
          va eng uzun sarlavha tarjimasi ikkalasi ham sig'adi.
        */
        header: t('incentives.date'),
        meta: { width: '130px' },
        cell: ({ getValue }) => <span className="tabular">{date(getValue())}</span>,
      }),

      helper.accessor('employeeName', {
        header: t('employee.fullName'),
        /*
          ⭐ BIR QATOR (egasi 2026-10-22): rasm va to'liq ism bir chiziqda,
          ism kesilmaydi — ustun eng uzun ismgacha kengayadi (`DataTable` —
          `whitespace-nowrap`). Boshqa ro'yxatlar (flags, probations,
          buyruqlar) bilan bir xil ko'rinish.
        */
        cell: ({ row }) => (
          <div className="flex items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId}
              photoKey={row.original.photoKey}
              name={row.original.employeeName}
              size="md"
            />
            <span className="font-medium">{row.original.employeeName}</span>
          </div>
        ),
      }),

      helper.accessor('typeName', {
        header: t('incentives.type'),
        /*
          Saralash YO'Q: tur nomi ikkita boshqa ma'lumotnomadan keladi
          (`AwardType` / `PenaltyType`) va u asosiy so'rovga umuman
          qo'shilmaydi — sahifa yig'ilgandan keyin alohida so'rov bilan
          olinadi (sababi `IncentiveReadModel.TypeNamesAsync` da). Ya'ni
          bazada bu ustun bo'yicha saralab bo'lmaydi, klient tomonda
          saralash esa faqat joriy 50 qatorni tartiblab, YOLG'ON natija
          berardi.
        */
        enableSorting: false,
        /*
          YAGONA `minWidth`li ustun — qoidaga ko'ra jadvalda bittadan
          ortiq bo'lmasligi kerak. Tur nomi ma'lumotnomadan (admin
          kiritadi), eng uzun kutilgan qiymat ~30 belgi va qat'iy
          kenglikka sig'maydi, shu sababli ortgan joyni aynan shu ustun
          oladi (`data-table.tsx` qoidasi).
        */
        meta: { minWidth: '240px' },
        cell: ({ row }) => {
          const look = INCENTIVE_KIND_LOOK[row.original.kind]
          const name = row.original.typeName ?? '—'

          return (
            <div className="flex items-center gap-2.5">
              <IconTile icon={look.icon} tone={look.iconTone} size="sm" />
              <span className="text-subhead font-medium">{name}</span>
              {/*
                Nishon (rag'bat / jarima) — nom bilan BIR qatorda (egasi
                2026-10-22), ilgari ostida edi. «Ташаккурнома» va «Мукофот»
                ikkalasi ham rag'bat, lekin ma'lumotnoma nomiga qarab buni
                bilib bo'lmaydi — nishon shu savolga javob beradi.
              */}
              <Badge tone={look.tone} size="sm">
                {t(look.labelKey)}
              </Badge>
            </div>
          )
        },
      }),

      helper.accessor('amount', {
        /*
          150px, o'ngga tekislangan — xodim kartasidagi tab bilan AYNAN
          bir xil (u yerdagi hisob-kitobga qarang: eng uzun realistik
          summa «12 000 000 so'm» ~115px + katak to'ldirishi 32px).
        */
        header: t('incentives.columnAmount'),
        enableSorting: false,
        meta: { align: 'end', width: '150px' },
        cell: ({ getValue, row }) => {
          const value = getValue()

          // Jarima summa o'rniga foiz bilan berilgan bo'lishi mumkin («4 %»).
          if (row.original.percent !== null) {
            return <span className="tabular">{percent(row.original.percent)}</span>
          }

          // Bo'sh katak chiziqcha bilan. Bu «ma'lumot yo'q» emas, DOMEN
          // QOIDASI: pul bilan bog'liq bo'lmagan turda (tashakkurnoma,
          // hayfsan) summa umuman bo'lishi mumkin emas.
          if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>

          return <span className="tabular">{money(value.amount, value.currency)}</span>
        },
      }),
    ],
    [date, money, percent, t],
  )

  /*
    Ruxsat holati sahifaning O'ZIDA, marshrutda `beforeLoad` bilan emas —
    `incidents-page.tsx` dagi bilan bir xil naqsh va bir xil sabab:
    server `GET /api/incentives` ni ham xuddi shu ruxsat bilan
    himoyalagan (403), ya'ni ekranni ochib qo'yish faqat bo'sh/xatoli
    jadval ko'rsatardi.

    Yon menyuda band umuman ko'rinmaydi (`nav-items.ts` dagi
    `permission`), lekin havolani qo'lda terib kelish mumkin.
  */
  if (!canView) {
    return (
      <div className="space-y-6">
        <PageHeader
          title={
            <span className="flex items-center gap-3">
              <IconTile icon={Award} tone="brand" size="lg" />
              {t('nav.incentives')}
            </span>
          }
        />
        <Card>
          <EmptyState
            icon={Award}
            title={t('incentives.noAccessTitle')}
            description={t('incentives.noAccessText')}
          />
        </Card>
      </div>
    )
  }

  const rows = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0
  const firstLoad = query.isPending
  const filtered = isFiltered(search)
  const listEmpty = rows.length === 0 && !firstLoad

  return (
    <div data-list-frame className="space-y-6 frame:flex frame:h-full frame:flex-col">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={Award} tone="brand" size="lg" />
            {t('nav.incentives')}
          </span>
        }
        subtitle={firstLoad ? t('common.loading') : t('incentives.subtitle', { n: num(total) })}
      />

      <Card className="overflow-hidden frame:flex frame:flex-1 frame:flex-col">
        <IncentivesToolbar
          filters={filters}
          onChange={changeFilters}
          options={options}
          // Ekrandagi AYNAN shu filtr va saralash — sahifasiz.
          onExport={canExport ? () => exportList.mutate(listFilters) : undefined}
          exporting={exportList.isPending}
        />

        {/* Eksport xatosi tugmaning ostida (xodimlar ro'yxatidagi kabi); 429 —
            alohida: yechimi kutish, filtrni o'zgartirish emas. */}
        {exportList.isError && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="danger" title={t('employees.exportFailed')}>
              {isRateLimited(exportList.error)
                ? t('employees.exportTooOften')
                : errorMessage(employeeErrorCode(exportList.error))}
            </Alert>
          </div>
        )}

        <DataTable
          fill
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          loading={firstLoad}
          skeletonRows={8}
          sorting={sorting}
          onSortingChange={changeSorting}
          onRowClick={openEmployee}
          rowLabel={(row) => row.employeeName}
          empty={
            listEmpty ? (
              filtered ? (
                <EmptyState
                  icon={SearchX}
                  title={t('incentives.notFoundTitle')}
                  description={t('incentives.notFoundText')}
                />
              ) : (
                <EmptyState
                  icon={Award}
                  title={t('incentives.listEmptyTitle')}
                  description={t('incentives.listEmptyText')}
                />
              )
            ) : null
          }
        />

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
 * kerak (`employees-page.tsx` dagi bilan bir xil).
 */
function sortingOf(param: IncentivesSortParam): SortingState {
  return param.startsWith('-')
    ? [{ id: param.slice(1), desc: true }]
    : [{ id: param, desc: false }]
}

/**
 * Jadval modeli → manzil.
 *
 * Saralash olib tashlansa ham, standart ustun tanlansa ham `undefined`
 * bo'ladi — ya'ni manzilda saralash umuman ko'rinmaydi va ro'yxat
 * standart tartibda (yangidan eskiga) chiziladi.
 */
function sortParamOf(sorting: SortingState): IncentivesSortParam | undefined {
  const first = sorting[0]
  if (first === undefined) return undefined

  return normalizeSort(first.desc ? `-${first.id}` : first.id)
}
