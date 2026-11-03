import { getRouteApi, useNavigate } from '@tanstack/react-router'
import { FileText, Inbox, Plus, Search, SearchX, UsersRound } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import {
  AttendanceEmployeePicker,
  type PickedEmployee,
} from '@/features/verification/components/attendance-employee-picker'
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
  // The picker needs a name; the address bar keeps only the id
  const [pickedEmployee, setPickedEmployee] = useState<PickedEmployee | null>(null)

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
        meta: { maxWidth: '260px' },
        cell: ({ getValue }) => <span title={getValue()}>{getValue()}</span>,
      }),
      helper.display({
        id: 'subject',
        header: t('blanks.list.subject'),
        cell: ({ row }) => <span className="font-medium">{row.original.subject.fullName ?? '—'}</span>,
      }),
      helper.accessor('departmentName', {
        header: t('employee.department'),
        enableSorting: false,
        meta: { maxWidth: '260px', hideOnMobile: true },
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
    () => (templates.data ?? []).map((item) => ({ value: item.code, label: `${item.code} · ${item.title}` })),
    [templates.data],
  )
  const statusOptions = useMemo(
    () => BLANK_STATUSES.map((status) => ({ value: status, label: t(STATUS_KEY[status]) })),
    [t],
  )

  const employeeValue: PickedEmployee | null =
    search.employee === undefined
      ? null
      : pickedEmployee?.id === search.employee
        ? pickedEmployee
        : { id: search.employee, fullName: t('blanks.list.employeeSelected'), photoKey: null, position: null, department: null }

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
        <div className="flex flex-wrap items-end gap-2.5 border-b border-[var(--border)] px-4 py-3.5 sm:gap-3 sm:px-5">
          <div className="min-w-[200px] flex-1 self-center">
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
          <div className="w-full sm:w-56">
            <Select
              label={t('blanks.list.template')}
              placeholder={t('common.all')}
              options={templateOptions}
              value={search.template ?? ''}
              onChange={(event) => patch({ template: event.target.value === '' ? undefined : event.target.value })}
            />
          </div>
          <div className="w-full sm:w-44">
            <Select
              label={t('blanks.list.status')}
              placeholder={t('common.all')}
              options={statusOptions}
              value={search.status ?? ''}
              onChange={(event) => {
                const value = event.target.value
                patch({ status: BLANK_STATUSES.find((status) => status === value) })
              }}
            />
          </div>
          <div className="w-full sm:w-56">
            <Select
              label={t('employee.department')}
              placeholder={t('common.all')}
              options={options.departments}
              value={search.department ?? ''}
              onChange={(event) => patch({ department: event.target.value === '' ? undefined : event.target.value })}
            />
          </div>
          <div className="w-full sm:w-64">
            <AttendanceEmployeePicker
              label={t('blanks.list.employee')}
              value={employeeValue}
              onChange={(next) => {
                setPickedEmployee(next)
                patch({ employee: next?.id })
              }}
            />
          </div>
          <div className="w-full sm:w-auto">
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
          <div className="flex w-full flex-wrap items-center gap-x-5 gap-y-2 self-center">
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
