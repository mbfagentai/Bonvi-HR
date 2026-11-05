import { getRouteApi } from '@tanstack/react-router'
import { ClipboardList, Inbox, Plus, Search, SearchX } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { REPORTS_EXPORT } from '@/features/employees/lib/employee-presentation'
import { useListExport } from '@/features/reports/api/list-export'
import { ListExportButton, ListExportError } from '@/features/reports/components/list-export'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { isDateRangeInverted, useSearchDraft } from '@/shared/lib/list-search'
import { PAGE_SIZE_ALL, useClampPage, usePageSize } from '@/shared/lib/use-page-size'
import {
  Alert,
  Badge,
  Button,
  Card,
  dataColumnHelper,
  DataTable,
  DateRangeField,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Pagination,
  SegmentedControl,
  type DataColumn,
} from '@/shared/ui'
import {
  VACANCY_REQUEST_STATUSES,
  vacancyRequestListParams,
  type VacancyRequest,
  type VacancyRequestListFilters,
} from '../api/vacancy-requests-api'
import { useVacancyRequests } from '../api/vacancy-requests-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { RECRUITING_VIEW } from '../lib/recruiting-presentation'
import {
  filtersToSearch,
  isFiltered,
  searchToFilters,
  searchToTab,
  type VacancyRequestsFilterState,
  type VacancyRequestsSearch,
  type VacancyRequestTab,
} from '../lib/vacancy-requests-search'
import { REQUEST_STATUS_TONE, VacancyRequestCreateDialog, VacancyRequestDialog } from './vacancy-request-dialogs'

const helper = dataColumnHelper<VacancyRequest>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: VacancyRequest[] = []

const route = getRouteApi('/_app/vakansiya-sorovlari')

/**
 * «Vakansiya so'rovlari» — bo'lim boshliqlarining yangi vakansiya so'rovlari
 * (`docs/SPEC-vakansiya-sorovi.md`). So'rov shu yerda kiritiladi, HR shu yerda vakansiya
 * ochadi yoki rad etadi. Sukut tab — «Yangi»: menyu aynan kutayotgan so'rovlar uchun ochiladi.
 */
export function VacancyRequestsPage() {
  const { t, num, date, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canExport = hasPermission(RECRUITING_VIEW) && hasPermission(REPORTS_EXPORT)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const page = search.page ?? 1
  const tab = searchToTab(search)
  const filters = useMemo(() => searchToFilters(search), [search])
  const { pageSize, setPageSize, allowAll } = usePageSize('vacancy-requests', true)

  const [creating, setCreating] = useState(false)
  const [openedId, setOpenedId] = useState<string | null>(null)

  const listFilters: VacancyRequestListFilters = {
    status: tab === 'all' ? undefined : tab,
    search: filters.search === '' ? undefined : filters.search,
    from: filters.from === '' ? undefined : filters.from,
    to: filters.to === '' ? undefined : filters.to,
  }

  const query = useVacancyRequests({ ...listFilters, page, pageSize })
  const exportList = useListExport()

  /*
   * `?id=` — «Mening ishlarim» qadamidan kelgan havola (`SPEC-mening-ishlarim.md` §11.6).
   * So'rov joriy tab yoki sahifada bo'lmasligi mumkin (masalan, tasdiqlangan), serverda esa
   * bitta so'rovni o'qish yo'q — shuning uchun faqat shunda holatsiz to'liq ro'yxatdan
   * izlanadi. So'rovlar soni oz, bu havola esa kamdan-kam ochiladi.
   */
  const linkedId = search.id
  const linkedOnPage = linkedId !== undefined && query.data?.items.some((item) => item.id === linkedId) === true
  const linkedLookup = useVacancyRequests(
    { page: 1, pageSize: PAGE_SIZE_ALL },
    linkedId !== undefined && !linkedOnPage && query.isSuccess,
  )
  const linkedItem =
    linkedId === undefined ? null : (linkedLookup.data?.items.find((item) => item.id === linkedId) ?? null)
  const linkedMissing = linkedId !== undefined && !linkedOnPage && linkedLookup.isSuccess && linkedItem === null

  const setSearch = useCallback(
    (next: (previous: VacancyRequestsSearch) => VacancyRequestsSearch, history: 'push' | 'replace' = 'replace') => {
      void navigate({ to: '/vakansiya-sorovlari', search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  const changeFilters = useCallback(
    (next: VacancyRequestsFilterState) => {
      setSearch((previous) => ({ ...previous, ...filtersToSearch(next), page: undefined }))
    },
    [setSearch],
  )

  const changeTab = useCallback(
    (next: VacancyRequestTab) => {
      setSearch((previous) => ({ ...previous, status: next === 'Pending' ? undefined : next, page: undefined }))
    },
    [setSearch],
  )

  const changePage = useCallback(
    (next: number) => setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push'),
    [setSearch],
  )

  const changePageSize = useCallback(
    (next: number) => {
      setPageSize(next)
      setSearch((previous) => ({ ...previous, page: undefined }))
    },
    [setPageSize, setSearch],
  )

  const clampPage = useCallback(
    (last: number) => setSearch((previous) => ({ ...previous, page: last <= 1 ? undefined : last })),
    [setSearch],
  )
  useClampPage(page, pageSize, query.isPlaceholderData ? undefined : query.data?.total, clampPage)

  const [draft, setDraft] = useSearchDraft(filters.search, (value) => changeFilters({ ...filters, search: value }))

  const columns = useMemo<Array<DataColumn<VacancyRequest>>>(
    () => [
      helper.accessor('createdAt', {
        header: t('vacancyRequests.createdAt'),
        enableSorting: false,
        cell: ({ getValue }) => <span className="tabular text-[var(--fg-secondary)]">{dateTime(getValue())}</span>,
      }),
      helper.accessor('title', {
        header: t('vacancyRequests.position'),
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex items-center gap-2">
            <span className="font-medium">{row.original.title}</span>
            <span className="tabular text-[var(--fg-secondary)]">× {num(row.original.openings)}</span>
          </span>
        ),
      }),
      helper.accessor('departmentName', {
        header: t('employee.department'),
        enableSorting: false,
        meta: { minWidth: '200px' },
        cell: ({ row }) => (
          <span className="text-[var(--fg-secondary)]" title={row.original.departmentPath ?? undefined}>
            {row.original.departmentName}
          </span>
        ),
      }),
      helper.accessor('requestedByName', {
        header: t('vacancyRequests.requestedBy'),
        enableSorting: false,
      }),
      helper.accessor('neededBy', {
        header: t('vacancyRequests.neededBy'),
        enableSorting: false,
        cell: ({ getValue }) => {
          const value = getValue()
          return value === null ? (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ) : (
            <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
          )
        },
      }),
      helper.accessor('status', {
        header: t('vacancyRequests.status'),
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex items-center gap-2">
            <Badge tone={REQUEST_STATUS_TONE[row.original.status]}>
              {t(`vacancyRequests.statuses.${row.original.status}`)}
            </Badge>
            {row.original.vacancyTitle !== null && (
              <span className="text-footnote text-[var(--fg-secondary)]">
                {t('vacancyRequests.vacancyProgress', {
                  candidates: num(row.original.candidateCount),
                  hired: num(row.original.hiredCount),
                })}
              </span>
            )}
          </span>
        ),
      }),
    ],
    [date, dateTime, num, t],
  )

  const items = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0
  const firstLoad = query.isPending
  const listEmpty = items.length === 0 && !firstLoad
  const opened =
    items.find((item) => item.id === (openedId ?? linkedId)) ?? (openedId === null ? linkedItem : null)

  const closeOpened = () => {
    setOpenedId(null)
    if (linkedId !== undefined) setSearch((previous) => ({ ...previous, id: undefined }))
  }

  return (
    <div data-list-frame className="space-y-6 frame:flex frame:h-full frame:flex-col">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={ClipboardList} tone="brand" size="lg" />
            {t('vacancyRequests.title')}
          </span>
        }
        subtitle={firstLoad ? t('common.loading') : t('vacancyRequests.subtitle', { n: num(total) })}
        actions={
          <Button variant="create" onClick={() => setCreating(true)}>
            <Plus />
            {t('vacancyRequests.create')}
          </Button>
        }
      />

      <SegmentedControl<VacancyRequestTab>
        value={tab}
        onChange={changeTab}
        aria-label={t('vacancyRequests.status')}
        options={[
          ...VACANCY_REQUEST_STATUSES.map((status) => ({
            value: status,
            label: t(`vacancyRequests.tabs.${status}`),
          })),
          { value: 'all' as const, label: t('common.all') },
        ]}
      />

      {query.isError && <Alert tone="danger">{errorMessage(recruitingErrorCode(query.error))}</Alert>}
      {linkedLookup.isError && <Alert tone="danger">{errorMessage(recruitingErrorCode(linkedLookup.error))}</Alert>}
      {linkedMissing && <Alert tone="warning">{t('vacancyRequests.linkedMissing')}</Alert>}

      <Card className="overflow-hidden frame:flex frame:flex-1 frame:flex-col">
        <div className="flex flex-wrap items-end gap-2.5 border-b border-[var(--border)] px-4 py-3.5 sm:gap-3 sm:px-5">
          <div className="min-w-[200px] flex-1 self-center">
            <Input
              icon={Search}
              shape="pill"
              type="search"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              placeholder={t('vacancyRequests.searchPlaceholder')}
              aria-label={t('common.search')}
            />
          </div>
          <div className="w-full sm:w-auto">
            <DateRangeField
              label={t('vacancyRequests.createdAt')}
              fromLabel={t('employments.dateFrom')}
              toLabel={t('employments.dateTo')}
              from={filters.from}
              to={filters.to}
              onFromChange={(value) => changeFilters({ ...filters, from: value })}
              onToChange={(value) => changeFilters({ ...filters, to: value })}
            />
          </div>
          {canExport && (
            <div className="self-center">
              <ListExportButton
                onClick={() =>
                  exportList.mutate({
                    path: '/api/recruiting/vacancy-requests/export',
                    params: vacancyRequestListParams(listFilters),
                    slug: 'vakansiya-sorovlari',
                  })
                }
                pending={exportList.isPending}
                disabled={isDateRangeInverted(filters)}
              />
            </div>
          )}
        </div>

        <ListExportError error={exportList.error} />

        <DataTable
          fill
          columns={columns}
          data={items}
          getRowId={(item) => item.id}
          loading={firstLoad}
          skeletonRows={6}
          onRowClick={(item) => setOpenedId(item.id)}
          rowLabel={(item) => item.title}
          empty={
            listEmpty ? (
              isFiltered(search) ? (
                <EmptyState
                  icon={SearchX}
                  title={t('vacancyRequests.notFoundTitle')}
                  description={t('vacancyRequests.notFoundText')}
                />
              ) : (
                <EmptyState
                  icon={Inbox}
                  title={t('vacancyRequests.emptyTitle')}
                  description={t('vacancyRequests.emptyText')}
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

      <VacancyRequestCreateDialog open={creating} onClose={() => setCreating(false)} />
      <VacancyRequestDialog item={opened} onClose={closeOpened} />
    </div>
  )
}
