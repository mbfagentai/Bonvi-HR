import { useNavigate } from '@tanstack/react-router'
import { ListFilter, Search, SearchX, X } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { FLAG_LOOK, REPORTS_EXPORT } from '@/features/employees/lib/employee-presentation'
import { useListExport } from '@/features/reports/api/list-export'
import { ListExportButton, ListExportError } from '@/features/reports/components/list-export'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  NO_DATE_RANGE,
  hasDateRange,
  isDateRangeInverted,
  useSearchDraft,
  type DateRange,
} from '@/shared/lib/list-search'
import { formatPhone } from '@/shared/lib/phone'
import { useClampPage, usePageSize } from '@/shared/lib/use-page-size'
import {
  Alert,
  Button,
  Card,
  DataTable,
  DateRangeField,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Pagination,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import {
  candidateListParams,
  type CandidateListFilters,
  type CandidateListItem,
} from '../api/recruiting-api'
import { useCandidates } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { RECRUITING_VIEW } from '../lib/recruiting-presentation'

const helper = dataColumnHelper<CandidateListItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: CandidateListItem[] = []

/**
 * ⭐ «Rezerv» bo'limidagi NOMZODLAR (foydalanuvchi qarori, 2026-09-26).
 *
 * Rezervga o'tkazilgan nomzod endi «Nomzodlar» ro'yxatida ko'rinmaydi
 * (`CandidateSearchFilter.HideReserve`) — uning o'rni shu yerda. U XODIM
 * EMAS: holati nomzodligicha qoladi va qator bosilganda NOMZOD kartasi
 * ochiladi, voronkaga qaytarish yoki ishga olish ham o'sha yerdan.
 *
 * Rezervdagi xodimlar (`EmployeeFlagType.Reserve`) bilan bitta jadvalga
 * ATAYLAB qo'shilmadi: ikkalasining ustunlari va amallari boshqa (xodimda
 * belgi sababi va yopish, nomzodda vakansiya, bosqich va keyingi aloqa).
 * Sahifa ularni `tabs` almashtirgichi bilan ajratadi.
 *
 * Holat (qidiruv, sahifa) marshrutdan PROPS bilan keladi —
 * `FlagListPage` dagi bilan bir xil sabab.
 */
export function ReserveCandidatesPage({
  search,
  onSearchChange,
  range,
  onRangeChange,
  page,
  onPageChange,
  tabs,
}: {
  /** Manzildagi qidiruv matni. Bo'sh satr — qidiruvsiz. */
  search: string
  onSearchChange: (next: string) => void
  /**
   * Ariza KELGAN sana oralig'i — manzildan (qidiruv bilan bir xil sabab).
   * Server bu holatda oraliqni ariza kuni bo'yicha kesadi, belgi qo'yilgan
   * kun bo'yicha emas (`SearchCandidatesQueryHandler` qoidasi).
   */
  range: DateRange
  onRangeChange: (next: DateRange) => void
  /** 1 dan. */
  page: number
  onPageChange: (next: number) => void
  /** «Nomzodlar | Xodimlar» almashtirgichi — sarlavhada turadi. */
  tabs: ReactNode
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const look = FLAG_LOOK.reserve
  const { hasPermission } = useAuth()
  // Eksport — nomzodlarni ko'rish VA `reports.export` (SPEC-excel-eksport §1)
  const canExport = hasPermission(RECRUITING_VIEW) && hasPermission(REPORTS_EXPORT)

  const [draft, setDraft] = useSearchDraft(search, onSearchChange)

  /*
   * Sana oralig'i yig'ilgan panelda — `FlagListPage` dagi bilan bir xil
   * joy va bir xil xatti-harakat: ikki bo'lim bir xil his qilsin.
   */
  const rangeCount = [range.from, range.to].filter((value) => value !== '').length
  const [rangeOpen, setRangeOpen] = useState(rangeCount > 0)

  // Teskari oraliq bilan so'rov yuborilMAYDI (`candidates-page.tsx` naqshi)
  const rangeInvalid = isDateRangeInverted(range)
  const { pageSize, setPageSize, allowAll } = usePageSize('reserve-candidates', true)

  /** Hajm o'zgarsa birinchi sahifadan — eski raqam yangi hajmda boshqa odamlar. */
  const changePageSize = (next: number) => {
    setPageSize(next)
    onPageChange(1)
  }

  /*
    `includeArchive` SHART: Excel'dan ko'chirilgan kengash arxivida ham
    rezervdagilar bor va server arxivni sukut bo'yicha yashiradi. Nomzodlar
    ro'yxatida rezerv endi umuman ko'rinmaydi, ya'ni bayroqsiz bu qatorlar
    tizimning hech bir ekranida chiqmasdi.
  */
  /*
    Filtr sahifasiz — ro'yxat ham, Excel eksporti ham AYNAN shundan: fayl
    ekrandagi bo'limdan boshqa nomzodni olib ketolmaydi.
  */
  const listFilters: CandidateListFilters = {
    status: 'Reserve',
    includeArchive: true,
    closedDate: true,
    search: search === '' ? undefined : search,
    from: range.from === '' ? undefined : range.from,
    to: range.to === '' ? undefined : range.to,
  }

  const query = useCandidates({ ...listFilters, page, pageSize }, !rangeInvalid)

  // Xato mutatsiyada qoladi — keyingi bosish uni o'zi tozalaydi (`employees-page.tsx`)
  const exportList = useListExport()
  // Teskari oraliqda eski sahifa ko'rinib qolmasin — u boshqa filtrniki
  const rows = rangeInvalid ? EMPTY : (query.data?.items ?? EMPTY)
  const total = rangeInvalid ? 0 : (query.data?.total ?? 0)

  // Eski sahifa raqami yangi hajmda bo'sh bo'lsa — oxirgi sahifaga (`useClampPage`)
  useClampPage(page, pageSize, query.isPlaceholderData ? undefined : query.data?.total, onPageChange)

  const columns = useMemo<Array<DataColumn<CandidateListItem>>>(
    () => [
      helper.accessor('fullName', {
        header: t('employee.fullName'),
        enableSorting: false,
        // Ism to'liq, bir qatorda (egasi 2026-10-22) — ustun ismgacha kengayadi
        meta: { minWidth: '260px' },
        cell: ({ getValue }) => <span className="font-medium">{getValue()}</span>,
      }),

      helper.accessor('vacancy', {
        header: t('recruiting.vacancy'),
        enableSorting: false,
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      // Qaysi bosqichda to'xtagan — voronkaga qaytganda o'sha yerdan davom etadi
      helper.accessor('currentStage', {
        header: t('recruiting.stage'),
        enableSorting: false,
        meta: { hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      // Rezervning ma'nosi «kerak bo'lganda chaqiramiz» — shu sababli
      // bu yerda (nomzodlar ro'yxatidan farqli) ustun bor
      helper.accessor('nextContactDate', {
        header: t('recruiting.nextContact'),
        enableSorting: false,
        meta: { width: '140px' },
        cell: ({ getValue }) => <DateCell value={getValue()} />,
      }),

      helper.accessor('phone', {
        header: t('employee.phone'),
        enableSorting: false,
        meta: { width: '170px', hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={formatPhone(getValue())} tabular />,
      }),

      helper.accessor('createdAt', {
        header: t('recruiting.createdAt'),
        enableSorting: false,
        meta: { width: '130px', hideOnMobile: true },
        cell: ({ getValue }) => <DateCell value={getValue()} />,
      }),
    ],
    [t],
  )

  return (
    <div data-list-frame className="space-y-5 frame:flex frame:h-full frame:flex-col">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={look.icon} tone={look.iconTone} />
            {t('flags.page.reserve.title')}
          </span>
        }
        subtitle={
          query.isSuccess
            ? t('flags.subtitle', { n: num(total) })
            : t('flags.page.reserve.hint')
        }
        // Tablar sahifada, avvalgi joyida qoladi (egasi 2026-10-22)
        toolbar={tabs}
      />

      {query.isError && (
        <Alert tone="danger">{errorMessage(recruitingErrorCode(query.error))}</Alert>
      )}

      <Card className="overflow-hidden frame:flex frame:flex-1 frame:flex-col">
        <div className="flex flex-wrap items-center gap-2.5 border-b border-[var(--border)] px-4 py-3.5 sm:gap-3 sm:px-5">
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
            variant={rangeOpen ? 'soft' : 'secondary'}
            aria-expanded={rangeOpen}
            onClick={() => setRangeOpen((previous) => !previous)}
          >
            <ListFilter />
            <span className="hidden sm:inline">{t('common.filters')}</span>
            {rangeCount > 0 && (
              <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
                {num(rangeCount)}
              </span>
            )}
          </Button>

          {/*
            Tozalash — «Filtrlar» yonida, faqat filtr qo'yilganda. Panel ostidagi
            alohida qator ro'yxatdan joy olardi (egasi 2026-10-22).
          */}
          {hasDateRange(range) && (
            <Button
              type="button"
              pill
              variant="ghost"
              title={t('employees.clearFilters')}
              aria-label={t('employees.clearFilters')}
              onClick={() => onRangeChange(NO_DATE_RANGE)}
            >
              <X />
              <span className="hidden xl:inline">{t('employees.clearFilters')}</span>
            </Button>
          )}

          {/* Eksport — «Filtrlar» yonida: fayl aynan shu qidiruv va oraliqqa tushadi */}
          {canExport && (
            <ListExportButton
              onClick={() =>
                exportList.mutate({
                  path: '/api/recruiting/candidates/export',
                  params: candidateListParams(listFilters),
                  slug: 'rezerv_nomzodlar',
                })
              }
              pending={exportList.isPending}
              disabled={rangeInvalid}
            />
          )}
        </div>

        {rangeOpen && (
          <div className="border-b border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {/* Nomzodlarda oraliq ARIZA kuni bo'yicha — yorliq shuni aytadi */}
              <DateRangeField
                label={t('dateRange.reserved')}
                fromLabel={t('recruiting.reservedFrom')}
                toLabel={t('recruiting.reservedTo')}
                from={range.from}
                to={range.to}
                onFromChange={(value) => onRangeChange({ ...range, from: value })}
                onToChange={(value) => onRangeChange({ ...range, to: value })}
              />
            </div>
          </div>
        )}

        {rangeInvalid && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="warning">{errorMessage('royxat.sana_oraligi_notogri')}</Alert>
          </div>
        )}

        <ListExportError error={exportList.error} />

        <DataTable
          fill
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          loading={query.isPending && !rangeInvalid}
          rowLabel={(row) => row.fullName}
          onRowClick={(row) =>
            void navigate({
              to: '/nomzodlar/$candidateId',
              params: { candidateId: row.id },
            })
          }
          /*
            To'rt holat, to'rtta javob:
              · so'rov yiqildi yoki oraliq teskari — bo'sh holat YO'Q:
                yuqoridagi xato yetarli, «rezervda hech kim yo'q» esa yolg'on
                bo'lardi;
              · sana oralig'i qo'yilgan, mos kelmadi — «oraliqda yo'q»;
              · qidiruv bor, mos kelmadi — «topilmadi» (HR nimanidir
                qidirgan, bo'lim bo'sh emas);
              · qidiruvsiz bo'sh — bo'limning o'z matni: rezervga qanday
                o'tkaziladi.

            «Topilmadi» izohi nomzodlar ro'yxatinikidan (`candidateNotFoundText`)
            ATAYLAB boshqa: u «Holati» filtriga yo'naltiradi, bu sahifada esa
            bunday filtr yo'q.
          */
          empty={
            // Oraliq qidiruvdan OLDIN: ikkalasi birga bo'lsa, toraytiruvchi
            // ko'pincha oraliq
            query.isError || rangeInvalid ? null : hasDateRange(range) ? (
              <EmptyState
                icon={SearchX}
                title={t('common.empty')}
                description={t('common.dateRangeEmptyText')}
              />
            ) : search !== '' ? (
              <EmptyState
                icon={SearchX}
                title={t('recruiting.candidateNotFoundTitle')}
                description={t('recruiting.reserveSection.notFoundText', { query: search.trim() })}
              />
            ) : (
              <EmptyState
                icon={look.icon}
                title={t('recruiting.reserveSection.emptyTitle')}
                description={t('recruiting.reserveSection.emptyText')}
              />
            )
          }
        />

        {/* `total > 0`: hajm tanlovi kichik ro'yxatda ham topilsin (`Pagination`) */}
        {total > 0 && (
          <Pagination
            page={page}
            pageSize={pageSize}
            total={total}
            onPageChange={onPageChange}
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

/** Qiymat to'liq, bir qatorda — jadval katagi qatorga bo'linmaydi (`DataTable`). */
function TextCell({ value, tabular = false }: { value: string | null; tabular?: boolean }) {
  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className={tabular ? 'tabular text-[var(--fg-secondary)]' : 'text-[var(--fg-secondary)]'}>{value}</span>
}

function DateCell({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null || value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
