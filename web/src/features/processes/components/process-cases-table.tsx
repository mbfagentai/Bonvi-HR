import { getRouteApi, useNavigate } from '@tanstack/react-router'
import { Inbox, ListFilter, Search, SearchX, X } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { useLocale } from '@/shared/i18n/use-locale'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage } from '@/shared/i18n/use-locale'
import { useSearchDraft } from '@/shared/lib/list-search'
import { todayIso } from '@/shared/lib/today'
import { useClampPage, usePageSize } from '@/shared/lib/use-page-size'
import {
  Alert,
  Button,
  Card,
  Checkbox,
  dataColumnHelper,
  DataTable,
  DateRangeField,
  EmptyState,
  Input,
  Pagination,
  Select,
  type DataColumn,
} from '@/shared/ui'

import {
  PROCESS_CASE_STATUSES,
  type ProcessCaseListItem,
  type ProcessCaseStatus,
} from '../api/processes-api'
import { useProcessCases } from '../api/processes-queries'
import {
  boardSearchToCaseFilters,
  clearBoardListFilters,
  hiddenBoardFilterCount,
  PROCESS_HOLDER_ROLES,
} from '../lib/process-board'
import { hasBoardListFilters } from '../lib/processes-search'
import {
  ProcessDueChip,
  ProcessHolderLabel,
  ProcessStatusBadge,
  useProcessRoleLabel,
} from './process-chips'

const route = getRouteApi('/_app/mening-ishlarim/')

const helper = dataColumnHelper<ProcessCaseListItem>()

/** Module level: a new array on each render would rebuild the table model. */
const EMPTY: ProcessCaseListItem[] = []

/**
 * The list view of «Jarayonlar» (SPEC §11.3, §10 #3): one line per case, under the project's one-line table rule. Mirrors
 * the probations toolbar: search and status in view, the rarer filters behind «Filtrlar».
 */
export function ProcessCasesTable({ process }: { process: string | undefined }) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const roleLabel = useProcessRoleLabel()
  const search = route.useSearch()
  const routeNavigate = route.useNavigate()
  const navigate = useNavigate()
  const { departments } = useFilterOptions()
  const { pageSize, setPageSize, allowAll } = usePageSize('process-cases', true)

  const hiddenCount = hiddenBoardFilterCount(search)
  const [panelOpen, setPanelOpen] = useState(hiddenCount > 0)

  const page = search.page ?? 1
  const filters = useMemo(() => boardSearchToCaseFilters(search, process), [search, process])
  const query = useProcessCases({ ...filters, page, pageSize })

  type Search = ReturnType<typeof route.useSearch>

  const patchSearch = useCallback(
    (patch: (previous: Search) => Search, history: 'push' | 'replace' = 'replace') => {
      void routeNavigate({ search: patch, replace: history === 'replace' })
    },
    [routeNavigate],
  )

  const setFilter = useCallback(
    (patch: Partial<Search>) => {
      patchSearch((previous) => ({ ...previous, ...patch, page: undefined }))
    },
    [patchSearch],
  )

  const [draft, setDraft] = useSearchDraft(search.q ?? '', (value) => {
    setFilter({ q: value === '' ? undefined : value })
  })

  const clampPage = useCallback(
    (last: number) => {
      patchSearch((previous) => ({ ...previous, page: last <= 1 ? undefined : last }))
    },
    [patchSearch],
  )
  useClampPage(page, pageSize, query.isPlaceholderData ? undefined : query.data?.total, clampPage)

  const today = todayIso()

  const columns = useMemo<Array<DataColumn<ProcessCaseListItem>>>(
    () => [
      helper.accessor((item) => item.subject.name, {
        id: 'subject',
        header: t('processes.board.columns.subject'),
        enableSorting: false,
        meta: { minWidth: '200px', maxWidth: '260px' },
        cell: ({ row }) => (
          <span className="font-medium">
            {row.original.subject.name ?? t('processes.board.noSubject')}
          </span>
        ),
      }),
      helper.accessor('processTitle', {
        header: t('processes.board.columns.process'),
        enableSorting: false,
        meta: { maxWidth: '260px' },
        cell: ({ getValue }) => <span className="text-[var(--fg-secondary)]">{getValue()}</span>,
      }),
      helper.accessor('departmentName', {
        header: t('processes.board.columns.department'),
        enableSorting: false,
        meta: { maxWidth: '260px' },
        cell: ({ getValue }) => (
          <span className="text-[var(--fg-secondary)]">{getValue() ?? '—'}</span>
        ),
      }),
      helper.accessor((item) => item.currentSteps[0]?.title ?? null, {
        id: 'step',
        header: t('processes.board.columns.step'),
        enableSorting: false,
        meta: { maxWidth: '260px' },
        cell: ({ row }) => {
          const [step, ...others] = row.original.currentSteps
          if (step === undefined) return <span className="text-[var(--fg-tertiary)]">—</span>
          return (
            <>
              {step.title}
              {others.length > 0 && (
                <span className="tabular text-[var(--fg-secondary)]"> +{num(others.length)}</span>
              )}
            </>
          )
        },
      }),
      helper.display({
        id: 'holder',
        header: t('processes.board.columns.holder'),
        meta: { maxWidth: '260px' },
        cell: ({ row }) => {
          const step = row.original.currentSteps[0]
          return step === undefined ? (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ) : (
            <ProcessHolderLabel holder={step.holder} className="text-[var(--fg-secondary)]" />
          )
        },
      }),
      helper.display({
        id: 'due',
        header: t('processes.board.columns.due'),
        cell: ({ row }) => {
          const step = row.original.currentSteps[0]
          return step === undefined ? null : (
            <ProcessDueChip
              dueDate={step.dueDate}
              daysLate={row.original.daysLate}
              today={today}
            />
          )
        },
      }),
      helper.accessor('status', {
        header: t('processes.board.columns.status'),
        enableSorting: false,
        cell: ({ getValue }) => <ProcessStatusBadge status={getValue()} />,
      }),
    ],
    [num, t, today],
  )

  const items = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0
  const firstLoad = query.isPending
  const filtered = hasBoardListFilters(search) || process !== undefined

  return (
    <div className="flex flex-col gap-4 frame:min-h-0 frame:flex-1">
      {query.isError && <Alert tone="danger">{errorMessage(problemCode(query.error))}</Alert>}

      <Card className="overflow-hidden frame:flex frame:flex-1 frame:flex-col">
        <div className="space-y-3 border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
          <div className="flex flex-wrap items-center gap-2.5 sm:gap-3">
            <div className="min-w-[200px] flex-1">
              <Input
                icon={Search}
                shape="pill"
                type="search"
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                placeholder={t('processes.board.searchPlaceholder')}
                aria-label={t('common.search')}
              />
            </div>

            <Select
              size="sm"
              aria-label={t('processes.board.columns.status')}
              className="w-full sm:w-[200px]"
              placeholder={t('processes.board.allStatuses')}
              options={PROCESS_CASE_STATUSES.map((status) => ({
                value: status,
                label: t(`processes.status.${status}`),
              }))}
              value={search.status ?? ''}
              onChange={(event) => {
                const { value } = event.target
                setFilter({ status: value === '' ? undefined : (value as ProcessCaseStatus) })
              }}
            />

            <Button
              type="button"
              pill
              variant={panelOpen ? 'soft' : 'secondary'}
              aria-expanded={panelOpen}
              onClick={() => setPanelOpen((previous) => !previous)}
            >
              <ListFilter />
              <span className="hidden sm:inline">{t('common.filters')}</span>
              {hiddenCount > 0 && (
                <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
                  {num(hiddenCount)}
                </span>
              )}
            </Button>

            {hasBoardListFilters(search) && (
              <Button
                type="button"
                pill
                variant="ghost"
                title={t('employees.clearFilters')}
                aria-label={t('employees.clearFilters')}
                onClick={() => patchSearch(clearBoardListFilters)}
              >
                <X />
                <span className="hidden xl:inline">{t('employees.clearFilters')}</span>
              </Button>
            )}
          </div>

          {panelOpen && (
            <div className="flex flex-wrap items-end gap-2.5 sm:gap-3">
              <Select
                size="sm"
                label={t('processes.board.columns.department')}
                className="w-full sm:w-[240px]"
                placeholder={t('processes.board.allDepartments')}
                options={departments}
                value={search.department ?? ''}
                onChange={(event) => {
                  const { value } = event.target
                  setFilter({ department: value === '' ? undefined : value })
                }}
              />
              <Select
                size="sm"
                label={t('processes.board.columns.holder')}
                className="w-full sm:w-[220px]"
                placeholder={t('processes.board.allHolders')}
                options={PROCESS_HOLDER_ROLES.map((role) => ({
                  value: role,
                  label: roleLabel(role),
                }))}
                value={search.holderRole ?? ''}
                onChange={(event) => {
                  const { value } = event.target
                  setFilter({ holderRole: value === '' ? undefined : value })
                }}
              />
              <div className="w-full sm:w-auto">
                <DateRangeField
                  label={t('processes.board.startedOn')}
                  fromLabel={t('employments.dateFrom')}
                  toLabel={t('employments.dateTo')}
                  from={search.from ?? ''}
                  to={search.to ?? ''}
                  onFromChange={(value) => setFilter({ from: value === '' ? undefined : value })}
                  onToChange={(value) => setFilter({ to: value === '' ? undefined : value })}
                />
              </div>
              <div className="self-center pt-5">
                <Checkbox
                  checked={search.late === true}
                  onChange={(checked) => setFilter({ late: checked ? true : undefined })}
                  label={t('processes.board.lateOnly')}
                />
              </div>
            </div>
          )}
        </div>

        <DataTable
          fill
          columns={columns}
          data={items}
          getRowId={(item) => item.id}
          loading={firstLoad}
          skeletonRows={6}
          onRowClick={(item) =>
            void navigate({ to: '/mening-ishlarim/$caseId', params: { caseId: item.id } })
          }
          rowLabel={(item) => item.subject.name ?? item.processTitle}
          empty={
            items.length === 0 && !firstLoad && !query.isError ? (
              filtered ? (
                <EmptyState
                  icon={SearchX}
                  title={t('processes.board.notFoundTitle')}
                  description={t('processes.board.notFoundText')}
                />
              ) : (
                <EmptyState
                  icon={Inbox}
                  title={t('processes.board.listEmptyTitle')}
                  description={t('processes.board.listEmptyText')}
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
            onPageChange={(next) =>
              patchSearch(
                (previous) => ({ ...previous, page: next <= 1 ? undefined : next }),
                'push',
              )
            }
            onPageSizeChange={(next) => {
              setPageSize(next)
              patchSearch((previous) => ({ ...previous, page: undefined }))
            }}
            allowAll={allowAll}
            disabled={query.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>
    </div>
  )
}
