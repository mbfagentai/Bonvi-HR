import { getRouteApi } from '@tanstack/react-router'
import { Briefcase, FileClock } from 'lucide-react'
import { useCallback, useMemo, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import type { MoneyValue } from '@/features/employees/api/employees-api'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { EVENT_LOOK, SALARY_VIEW } from '@/features/employees/lib/employee-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import {
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  Pagination,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import type { EmploymentEventItem } from '../api/employments-api'
import { useEmploymentEvents, useFilterOptions } from '../api/employments-queries'
import {
  filtersToSearch,
  isFiltered,
  searchToFilters,
  type EmploymentEventsFilterState,
  type EmploymentEventsSearch,
} from '../lib/employments-search'
import { EmploymentsToolbar } from './employments-toolbar'

const helper = dataColumnHelper<EmploymentEventItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: EmploymentEventItem[] = []

/**
 * Bir sahifadagi yozuvlar soni.
 *
 * Xodimlar ro'yxati bilan bir xil (50) — u yerdagi hisob-kitob bu yerga
 * ham to'g'ri keladi: server chegarasi 200, ekran esa imkon qadar ko'p
 * hodisani bir sahifada ko'rsatishni xohlaydi.
 */
const PAGE_SIZE = 50

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/buyruqlar')

/**
 * Buyruqlar — barcha xodimlar bo'yicha umumiy oqim.
 *
 * Excel'da bu to'rtta alohida varaq edi: yangi kelganlar, lavozimi
 * o'zgarganlar, qo'shimcha vazifalar, ishdan ketganlar. Bu yerda esa
 * bitta jadval va uning filtrlangan ko'rinishlari — HR «shu oyda qanday
 * buyruqlar chiqdi?» degan savolga bitta ekrandan javob oladi.
 *
 * Xodim kartasidagi mehnat tarixi lentasidan farqi: bu yerda BARCHA
 * xodimlar aralash, buyruq raqami bo'yicha qidirish va sana oralig'i
 * bilan filtrlash mumkin.
 */
export function EmploymentEventsPage() {
  const { t, num } = useLocale()
  const { hasPermission } = useAuth()
  const canSeeSalary = hasPermission(SALARY_VIEW)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const page = search.page ?? 1
  const filters = useMemo(() => searchToFilters(search), [search])
  const options = useFilterOptions()

  const query = useEmploymentEvents({
    search: filters.search,
    type: filters.type === '' ? undefined : filters.type,
    from: filters.from === '' ? undefined : filters.from,
    to: filters.to === '' ? undefined : filters.to,
    departmentId: filters.departmentId === '' ? undefined : filters.departmentId,
    legalEntityId: filters.legalEntityId === '' ? undefined : filters.legalEntityId,
    page,
    pageSize: PAGE_SIZE,
  })

  /** Standart holatda `replace` — filtr aniqlashtirish, alohida qadam emas. */
  const setSearch = useCallback(
    (
      next: (previous: EmploymentEventsSearch) => EmploymentEventsSearch,
      history: 'push' | 'replace' = 'replace',
    ) => {
      void navigate({ to: '/buyruqlar', search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  /** Filtr o'zgarganda 7-sahifada qolib ketmaslik kerak — u endi bo'sh. */
  const changeFilters = useCallback(
    (next: EmploymentEventsFilterState) => {
      setSearch((previous) => ({ ...previous, ...filtersToSearch(next), page: undefined }))
    },
    [setSearch],
  )

  const changePage = useCallback(
    (next: number) => {
      setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push')
    },
    [setSearch],
  )

  const columns = useMemo<Array<DataColumn<EmploymentEventItem>>>(
    () => [
      helper.accessor('employeeName', {
        header: t('employee.fullName'),
        /*
          Boshqa ro'yxatlar (flags, probations) bilan BIR XIL 300px:
          300px − katak to'ldirishi (px-4 × 2 = 32px) = 268px; − rasm
          (size="lg" = 48px) − oraliq (gap-3 = 12px) = 208px sof matnga.
          Uch ro'yxatda bir xil odam bir xil kenglikda ko'rinsin.
        */
        meta: { width: '300px' },
        cell: ({ row }) => (
          <div className="flex min-w-0 items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId}
              photoKey={row.original.photoKey}
              name={row.original.employeeName}
              size="lg"
            />
            <p className="truncate-1 font-medium">{row.original.employeeName}</p>
          </div>
        ),
      }),

      helper.accessor('type', {
        header: t('employments.type'),
        enableSorting: false,
        /*
          216px. Hisob: katak to'ldirishi (32px) + IconTile "sm" (32px) +
          oraliq (gap-2.5 = 10px) = 74px xizmat uchun, 142px matnga qoladi.
          Eng uzun tarjima — o'zbekcha «Lavozim o'zgarishi» / «Qayta ishga
          qabul» (18 belgi). Sarlavha kichik shriftda (`text-caption2`,
          uppercase, tracking-wider) bo'lgani uchun sig'adi; matn esa
          `text-subhead` (~8px/belgi) da 142px ga ~17-18 belgi sig'adi —
          chetki holatlar `truncate-1` + `title` bilan qoplanadi.
        */
        meta: { width: '216px' },
        cell: ({ row }) => {
          const look = EVENT_LOOK[row.original.type]
          const label = t(`employees.events.${row.original.type}`)

          return (
            <span className="flex min-w-0 items-center gap-2.5" title={label}>
              <IconTile icon={look.icon} tone={look.tone} size="sm" />
              <span className="truncate-1 text-subhead">{label}</span>
            </span>
          )
        },
      }),

      helper.accessor('effectiveDate', {
        /*
          Sarlavha ATAYLAB qisqa: to'liq «Kuchga kirish sanasi»
          ustunni ikki barobar kengaytirardi. 130px — probations-page
          dagi sana ustunlari bilan bir xil (o'sha yerdagi hisob-kitobga
          qarang): «25.08.2026» (10 belgi, tabular) + sarlavha eng uzun
          tarjimasi («Кучга кириш», 11 belgi) ikkalasi ham sig'adi.
        */
        header: t('employments.effectiveDateShort'),
        meta: { width: '130px' },
        cell: ({ getValue }) => <DateCell value={getValue()} />,
      }),

      helper.accessor('orderNo', {
        // Qisqa sarlavha (`employees.orderShort`) — buyruq raqami
        // odatda qisqa qator, uzun sarlavhaga o'ralib ketmasin.
        header: t('employees.orderShort'),
        enableSorting: false,
        // 130px: erkin matn (HR qo'lda kiritadi), uzunroq qiymatlar
        // `truncate-1` + `title` bilan qoplanadi.
        meta: { width: '130px' },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('orderDate', {
        header: t('employee.orderDate'),
        // 140px — sarlavha «Буйруқ санаси»/«Номер приказа» uzunroq
        // (13 belgi), effectiveDate ustunidan 10px kengroq shu sababli.
        meta: { width: '140px', hideOnMobile: true },
        cell: ({ getValue }) => <DateCell value={getValue()} />,
      }),

      helper.display({
        id: 'details',
        header: t('employments.details'),
        enableSorting: false,
        /*
          YAGONA `minWidth`li ustun — qoidaga ko'ra jadvalda bittadan
          ortiq bo'lmasligi kerak. Bo'lim, lavozim, oklad va izoh —
          uzunligi oldindan noma'lum matnlar, shu sababli ortgan joyni
          aynan shu ustun oladi (`data-table.tsx` qoidasi).
        */
        meta: { minWidth: '260px' },
        cell: ({ row }) => (
          <DetailsCell event={row.original} canSeeSalary={canSeeSalary} />
        ),
      }),
    ],
    [canSeeSalary, t],
  )

  const events = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0
  const firstLoad = query.isPending
  const filtered = isFiltered(search)
  const listEmpty = events.length === 0 && !firstLoad

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={Briefcase} tone="brand" size="lg" />
            {t('employments.title')}
          </span>
        }
        subtitle={firstLoad ? t('common.loading') : t('employments.subtitle', { n: num(total) })}
      />

      <Card className="overflow-hidden">
        <EmploymentsToolbar filters={filters} onChange={changeFilters} options={options} />

        <DataTable
          columns={columns}
          data={events}
          getRowId={(event) => event.id}
          loading={firstLoad}
          skeletonRows={8}
          rowLabel={(event) => event.employeeName}
          empty={
            listEmpty ? (
              filtered ? (
                <EmptyState
                  icon={FileClock}
                  title={t('employments.notFoundTitle')}
                  description={t('employments.notFoundText')}
                />
              ) : (
                <EmptyState
                  icon={Briefcase}
                  title={t('employments.emptyTitle')}
                  description={t('employments.emptyText')}
                />
              )
            ) : null
          }
        />

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
    </div>
  )
}

/** Bo'sh katak chiziqcha bilan — «yuklanmadimi?» degan savol tug'ilmasin. */
function TextCell({ value }: { value: string | null }) {
  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return (
    <span className="truncate-1 text-[var(--fg-secondary)]" title={value}>
      {value}
    </span>
  )
}

function DateCell({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}

/**
 * «Tafsilot» ustuni — nima o'zgargani. Bo'lim, lavozim, oklad, izoh:
 * har bir hodisa turi bularning faqat bir qismini to'ldiradi
 * (`employee-timeline-tab.tsx` dagi lentaning ixcham ko'rinishi).
 *
 * Ko'pi bilan IKKITA qator: qator ichida ustun kengligi cheklangan,
 * uchinchisi bo'lganda ham ekran «to'lib toshgan» taassurot bermasin.
 */
function DetailsCell({
  event,
  canSeeSalary,
}: {
  event: EmploymentEventItem
  canSeeSalary: boolean
}) {
  const { t } = useLocale()

  const lines: ReactNode[] = []

  if (event.toPosition !== null && event.toPosition !== event.fromPosition) {
    lines.push(
      <ChangeLine
        key="position"
        label={t('employee.position')}
        from={event.fromPosition}
        to={event.toPosition}
      />,
    )
  }

  if (event.toDepartment !== null && event.toDepartment !== event.fromDepartment) {
    lines.push(
      <ChangeLine
        key="department"
        label={t('employee.department')}
        from={event.fromDepartment}
        to={event.toDepartment}
      />,
    )
  }

  if (lines.length < 2 && event.toSalary !== null) {
    lines.push(
      canSeeSalary ? (
        <MoneyChangeLine
          key="salary"
          label={t('employee.salary')}
          from={event.fromSalary}
          to={event.toSalary}
        />
      ) : (
        <span key="salary" className="truncate-1 text-caption1 text-[var(--fg-tertiary)]">
          {t('employees.salaryHiddenShort')}
        </span>
      ),
    )
  }

  if (lines.length < 2 && event.note !== null) {
    lines.push(
      <span
        key="note"
        className="truncate-1 text-caption1 text-[var(--fg-secondary)]"
        title={event.note}
      >
        {event.note}
      </span>,
    )
  }

  if (lines.length === 0) return <span className="text-[var(--fg-tertiary)]">—</span>

  return <div className="space-y-0.5">{lines.slice(0, 2)}</div>
}

function ChangeLine({ label, from, to }: { label: string; from: string | null; to: string }) {
  return (
    <p className="truncate-1 flex items-baseline gap-1 text-footnote">
      <span className="shrink-0 text-[var(--fg-tertiary)]">{label}:</span>
      <span className="truncate-1 font-medium">
        {from !== null && `${from} → `}
        {to}
      </span>
    </p>
  )
}

function MoneyChangeLine({
  label,
  from,
  to,
}: {
  label: string
  from: MoneyValue | null
  to: MoneyValue
}) {
  const { money } = useLocale()

  const fromText = from === null ? null : money(from.amount, from.currency)
  const toText = money(to.amount, to.currency)

  return (
    <p className="tabular truncate-1 flex items-baseline gap-1 text-footnote">
      <span className="shrink-0 text-[var(--fg-tertiary)]">{label}:</span>
      <span className="truncate-1 font-medium">
        {fromText !== null && fromText !== toText && `${fromText} → `}
        {toText}
      </span>
    </p>
  )
}
