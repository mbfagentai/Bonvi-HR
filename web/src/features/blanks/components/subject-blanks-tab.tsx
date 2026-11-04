import { useNavigate } from '@tanstack/react-router'
import { FileText, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import type { PickedEmployee } from '@/features/verification/components/attendance-employee-picker'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import { Alert, Badge, Button, Card, dataColumnHelper, DataTable, EmptyState, Pagination, type DataColumn } from '@/shared/ui'

import type { BlankListItem } from '../api/blanks-api'
import { useBlankSubmissions } from '../api/blanks-queries'
import { blankErrorCode } from '../lib/blank-error'
import { BLANKS_FILL, blankNumberLabel } from '../lib/blank-permissions'
import { STATUS_KEY, STATUS_TONE } from '../lib/blank-presentation'
import { BlankCreateDialog } from './blank-create-dialog'

const helper = dataColumnHelper<BlankListItem>()
const PAGE_SIZE = 20

/**
 * Employee card tab «Blankalar» (SPEC-blankalar §12): every blank about this employee,
 * newest first, one line each; «Yangi blanka» opens the create dialog preset to the person.
 * The tab itself is rendered only with `blanks.view`; the button only with `blanks.fill`.
 */
export function SubjectBlanksTab({ employee }: { employee: PickedEmployee }) {
  const { t, num, date, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canFill = hasPermission(BLANKS_FILL)

  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const query = useBlankSubmissions({ employeeId: employee.id, page, pageSize: PAGE_SIZE })
  const items = query.data?.items ?? []
  const total = query.data?.total ?? 0
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
      helper.accessor('dueDate', {
        header: t('blanks.list.dueDate'),
        enableSorting: false,
        cell: ({ row }) => {
          const due = row.original.dueDate
          if (due === null) return <span className="text-[var(--fg-tertiary)]">—</span>
          const ended = row.original.status !== 'Draft' && row.original.status !== 'InApproval'
          return (
            <span className={!ended && due < today ? 'tabular font-medium text-ios-red' : 'tabular text-[var(--fg-secondary)]'}>
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
    [date, dateTime, t, today],
  )

  return (
    <Card className="space-y-4 p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-headline font-semibold">
          {t('employees.tabBlanks')}
          {query.data !== undefined && (
            <span className="ml-2 text-subhead font-normal text-[var(--fg-secondary)]">{num(total)}</span>
          )}
        </h2>
        {canFill && (
          <Button variant="primary" size="sm" onClick={() => setCreating(true)}>
            <Plus />
            {t('blanks.list.create')}
          </Button>
        )}
      </div>

      {query.isError && <Alert tone="danger">{errorMessage(blankErrorCode(query.error))}</Alert>}

      <DataTable
        columns={columns}
        data={items}
        getRowId={(item) => item.id}
        loading={query.isPending}
        skeletonRows={3}
        onRowClick={(item) => void navigate({ to: '/blankalar/$submissionId', params: { submissionId: item.id } })}
        rowLabel={(item) => `${blankNumberLabel(item.templateCode, item.number)} ${item.templateTitle}`}
        empty={
          query.isSuccess && items.length === 0 ? (
            <EmptyState icon={FileText} title={t('blanks.view.subjectEmptyTitle')} description={t('blanks.view.subjectEmptyText')} />
          ) : null
        }
      />

      {total > PAGE_SIZE && <Pagination page={page} pageSize={PAGE_SIZE} total={total} onPageChange={setPage} />}

      {creating && <BlankCreateDialog employee={employee} onClose={() => setCreating(false)} />}
    </Card>
  )
}
