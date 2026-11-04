import { getRouteApi, useNavigate } from '@tanstack/react-router'
import { FileText, Inbox, ListFilter, Plus, Search, SearchX, UsersRound, X } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { useSearchDraft } from '@/shared/lib/list-search'
import { useClampPage, usePageSize } from '@/shared/lib/use-page-size'
import {
  Alert,
  Badge,
  Button,
  Card,
  Checkbox,
  dataColumnHelper,
  DataTable,
  DateRangeField,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Pagination,
  Select,
  type DataColumn,
} from '@/shared/ui'

import { BLANK_STATUSES, type BlankListItem } from '../api/blanks-api'
import { useBlankSubmissions, useBlankTemplates } from '../api/blanks-queries'
import { blankErrorCode } from '../lib/blank-error'
import { todayIso } from '@/shared/lib/today'
import { BLANKS_FILL, BLANKS_MANAGE, blankNumberLabel } from '../lib/blank-permissions'
import { STATUS_KEY, STATUS_TONE } from '../lib/blank-presentation'
import {
  isBlanksFiltered,
  searchToListFilters,
  type BlanksSearch,
} from '../lib/blanks-search'
import { BlankCreateDialog } from './blank-create-dialog'
import { BlankRolesDialog } from './blank-roles-dialog'

const helper = dataColumnHelper<BlankListItem>()

/** Module level — a new array on every render would rebuild the table model. */
const EMPTY: BlankListItem[] = []

const route = getRouteApi('/_app/blankalar/')

type Flag = 'waiting' | 'incomplete' | 'overdue'
const FLAGS: readonly Flag[] = ['waiting', 'incomplete', 'overdue']

/**
 * «Blankalar» — every HR blank (SPEC-blankalar §7 #6). One-line rows, title + count in
 * the top bar, pagination 20/50/100/«Barchasi» (project table rule). A row opens the
 * submission: the fill page for a draft, the read-only view otherwise.
 */
export function BlanksPage() {
  const { t, num, date, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canFill = hasPermission(BLANKS_FILL)
  const canManage = hasPermission(BLANKS_MANAGE)

  const search = route.useSearch()
  const navigate = route.useNavigate()
  const goTo = useNavigate()

  const page = search.page ?? 1
  const { pageSize, setPageSize, allowAll } = usePageSize('blanks', true)

  const [creating, setCreating] = useState(false)
  const [editingRoles, setEditingRoles] = useState(false)

  const templates = useBlankTemplates()
  const options = useFilterOptions()
  const query = useBlankSubmissions({ ...searchToListFilters(search), page, pageSize })

  const setSearch = useCallback(
    (next: (previous: BlanksSearch) => BlanksSearch, history: 'push' | 'replace' = 'replace') => {
      void navigate({ to: '/blankalar', search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  const patch = useCallback(
    (changes: Partial<BlanksSearch>) => setSearch((previous) => ({ ...previous, ...changes, page: undefined })),
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

  const [draft, setDraft] = useSearchDraft(search.q ?? '', (value) => patch({ q: value === '' ? undefined : value }))

  const today = todayIso()

  const columns = useMemo<Array<DataColumn<BlankListItem>>>(
    () => [
      helper.accessor('number', {
        header: t('blanks.list.number'),
        enableSorting: false,
        cell: ({ row }) => (
          <span className="tabular whitespace-nowrap font-medium">
            {blankNumberLabel(row.original.templateCode, row.original.number)}
          </span>
        ),
      }),
      helper.accessor('templateTitle', {
        header: t('blanks.list.template'),
        enableSorting: false,
        meta: { maxWidth: '220px' },
        cell: ({ getValue }) => <span title={getValue()}>{getValue()}</span>,
      }),
      helper.display({
        id: 'subject',
        header: t('blanks.list.subject'),
        meta: { maxWidth: '260px' },
        cell: ({ row }) => <span className="font-medium">{row.original.subject.fullName ?? '—'}</span>,
      }),
      helper.accessor('departmentName', {
        header: t('employee.department'),
        enableSorting: false,
        meta: { maxWidth: '200px', hideOnMobile: true },
        cell: ({ getValue }) => (
          <span className="text-[var(--fg-secondary)]" title={getValue() ?? undefined}>
            {getValue() ?? '—'}
          </span>
        ),
      }),
      helper.accessor('status', {
        header: t('blanks.list.status'),
        enableSorting: false,
        cell: ({ row }) => <Badge tone={STATUS_TONE[row.original.status]}>{t(STATUS_KEY[row.original.status])}</Badge>,
      }),
      helper.display({
        id: 'currentSteps',
        header: t('blanks.list.currentStep'),
        meta: { maxWidth: '260px', hideOnMobile: true },
        cell: ({ row }) => {
          const titles = row.original.currentSteps.map((step) => step.title).join(', ')
          return titles === '' ? (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ) : (
            <span className="text-[var(--fg-secondary)]" title={titles}>
              {titles}
            </span>
          )
        },
      }),
      helper.accessor('missingCount', {
        header: t('blanks.list.missing'),
        enableSorting: false,
        cell: ({ getValue }) =>
          getValue() > 0 ? (
            <Badge tone="warning">{t('blanks.list.missingN', { n: num(getValue()) })}</Badge>
          ) : (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ),
      }),
      helper.accessor('dueDate', {
        header: t('blanks.list.dueDate'),
        enableSorting: false,
        cell: ({ row }) => {
          const due = row.original.dueDate
          if (due === null) return <span className="text-[var(--fg-tertiary)]">—</span>
          const ended = row.original.status !== 'Draft' && row.original.status !== 'InApproval'
          const late = !ended && due < today
          return (
            <span className={late ? 'tabular font-medium text-ios-red' : 'tabular text-[var(--fg-secondary)]'}>
              {date(due)}
            </span>
          )
        },
      }),
      helper.accessor('createdAt', {
        header: t('blanks.list.createdAt'),
        enableSorting: false,
        meta: { hideOnMobile: true },
        cell: ({ getValue }) => <span className="tabular text-[var(--fg-secondary)]">{dateTime(getValue())}</span>,
      }),
    ],
    [date, dateTime, num, t, today],
  )

  const templateOptions = useMemo(
    () => [
      { value: '', label: t('blanks.list.allTemplates') },
      ...(templates.data ?? []).map((item) => ({ value: item.code, label: `${item.code} · ${item.title}` })),
    ],
    [templates.data, t],
  )
  const statusOptions = useMemo(
    () => [
      { value: '', label: t('blanks.list.allStatuses') },
      ...BLANK_STATUSES.map((status) => ({ value: status, label: t(STATUS_KEY[status]) })),
    ],
    [t],
  )
  const departmentOptions = useMemo(
    () => [{ value: '', label: t('common.all') }, ...options.departments],
    [options.departments, t],
  )

  // Panel fields: department, created range, the three flags
  const panelCount = [search.department, search.from, search.to, ...FLAGS.map((flag) => search[flag])].filter(
    (value) => value !== undefined,
  ).length
  const [panelOpen, setPanelOpen] = useState(panelCount > 0)

  const items = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0
  const firstLoad = query.isPending
  const listEmpty = items.length === 0 && !firstLoad

  return (
    <div data-list-frame className="space-y-6 frame:flex frame:h-full frame:flex-col">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={FileText} tone="brand" size="lg" />
            {t('blanks.list.title')}
          </span>
        }
        subtitle={firstLoad ? t('common.loading') : t('blanks.list.subtitle', { n: num(total) })}
        actions={
          <>
            {canManage && (
              <Button onClick={() => setEditingRoles(true)}>
                <UsersRound />
                {t('blanks.roles.button')}
              </Button>
            )}
            {canFill && (
              <Button variant="create" onClick={() => setCreating(true)}>
                <Plus />
                {t('blanks.list.create')}
              </Button>
            )}
          </>
        }
      />

      {query.isError && <Alert tone="danger">{errorMessage(blankErrorCode(query.error))}</Alert>}

      <Card className="overflow-hidden frame:flex frame:flex-1 frame:flex-col">
        <div className="flex flex-wrap items-center gap-2.5 border-b border-[var(--border)] p-4 sm:gap-3">
          <div className="min-w-[200px] flex-1">
            <Input
              icon={Search}
              shape="pill"
              type="search"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              placeholder={t('blanks.list.searchPlaceholder')}
              aria-label={t('common.search')}
            />
          </div>

          <Select
            size="sm"
            aria-label={t('blanks.list.template')}
            options={templateOptions}
            value={search.template ?? ''}
            onChange={(event) => patch({ template: event.target.value === '' ? undefined : event.target.value })}
            className="w-full sm:w-[260px]"
          />

          <Select
            size="sm"
            aria-label={t('blanks.list.status')}
            options={statusOptions}
            value={search.status ?? ''}
            onChange={(event) => {
              const value = event.target.value
              patch({ status: BLANK_STATUSES.find((status) => status === value) })
            }}
            className="w-full sm:w-[200px]"
          />

          {/* «Filtrlar» — the rarely changed ones live in a collapsible panel (`probations-page.tsx` pattern) */}
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

          {isBlanksFiltered(search) && (
            <Button
              type="button"
              pill
              variant="ghost"
              title={t('employees.clearFilters')}
              aria-label={t('employees.clearFilters')}
              onClick={() => {
                setDraft('')
                setSearch(() => ({}))
              }}
            >
              <X />
              <span className="hidden xl:inline">{t('employees.clearFilters')}</span>
            </Button>
          )}
        </div>

        {panelOpen && (
          <div className="space-y-4 border-b border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              <Select
                label={t('employee.department')}
                options={departmentOptions}
                value={search.department ?? ''}
                onChange={(event) => patch({ department: event.target.value === '' ? undefined : event.target.value })}
              />
              <DateRangeField
                label={t('blanks.list.createdAt')}
                fromLabel={t('employments.dateFrom')}
                toLabel={t('employments.dateTo')}
                from={search.from ?? ''}
                to={search.to ?? ''}
                onFromChange={(value) => patch({ from: value === '' ? undefined : value })}
                onToChange={(value) => patch({ to: value === '' ? undefined : value })}
              />
            </div>
            <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
              {FLAGS.map((flag) => (
                <Checkbox
                  key={flag}
                  checked={search[flag] === true}
                  onChange={(checked) => patch({ [flag]: checked ? true : undefined })}
                  label={t(`blanks.list.flags.${flag}`)}
                />
              ))}
            </div>
          </div>
        )}

        <DataTable
          fill
          columns={columns}
          data={items}
          getRowId={(item) => item.id}
          loading={firstLoad}
          skeletonRows={6}
          onRowClick={(item) => void goTo({ to: '/blankalar/$submissionId', params: { submissionId: item.id } })}
          rowLabel={(item) => `${blankNumberLabel(item.templateCode, item.number)} ${item.subject.fullName ?? ''}`}
          empty={
            listEmpty ? (
              isBlanksFiltered(search) ? (
                <EmptyState icon={SearchX} title={t('blanks.list.notFoundTitle')} description={t('blanks.list.notFoundText')} />
              ) : (
                <EmptyState icon={Inbox} title={t('blanks.list.emptyTitle')} description={t('blanks.list.emptyText')} />
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

      {creating && <BlankCreateDialog onClose={() => setCreating(false)} />}
      {editingRoles && <BlankRolesDialog onClose={() => setEditingRoles(false)} />}
    </div>
  )
}
