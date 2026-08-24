import { CalendarClock, CircleAlert, Timer } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { EMPLOYMENT_MANAGE } from '@/features/employees/lib/employee-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  Select,
  dataColumnHelper,
  type BadgeTone,
  type DataColumn,
} from '@/shared/ui'

import {
  PROBATION_STATUSES,
  type ProbationFilters,
  type ProbationItem,
  type ProbationStatus,
} from '../api/probations-api'
import { WARNING_DAYS, useEndingSoon, useProbations } from '../api/probations-queries'
import { ExtendDialog, ResolveDialog } from './probation-dialogs'

const helper = dataColumnHelper<ProbationItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: ProbationItem[] = []

const STATUS_TONE: Readonly<Record<ProbationStatus, BadgeTone>> = {
  inProgress: 'info',
  passed: 'success',
  failed: 'danger',
  leftEarly: 'warning',
  cancelled: 'neutral',
}

/**
 * Sinov muddatidagi xodimlar.
 *
 * HR bu ekrandan bitta narsani so'ragan: <b>muddat tugashiga 7 kun qolganda
 * ogohlantirish</b>. Excel'da bu qo'lda kuzatilardi va shu sababli tez-tez
 * o'tkazib yuborilardi.
 *
 * Ogohlantirish ATAYLAB fon vazifasi (cron) orqali emas, sahifaning
 * o'zidan chiqadi: tizimdan atigi bir necha kishi foydalanadi va ular uni
 * har kuni ochadi. Jonli so'rov doim aniq — kechagi vazifa natijasi emas —
 * va «vazifa ishlamay qolgan, hech kim sezmagan» degan xato holati umuman
 * paydo bo'lmaydi.
 */
export function ProbationsPage() {
  const { t, num } = useLocale()
  const { hasPermission } = useAuth()

  const canManage = hasPermission(EMPLOYMENT_MANAGE)

  const [filters, setFilters] = useState<ProbationFilters>({ status: '' })
  const [resolving, setResolving] = useState<ProbationItem | null>(null)
  const [extending, setExtending] = useState<ProbationItem | null>(null)

  const list = useProbations(filters)
  const warning = useEndingSoon()

  const rows = list.data ?? EMPTY
  const soonCount = warning.data?.length ?? 0

  const statusOptions = useMemo(
    () => [
      { value: '', label: t('probation.allStatuses') },
      ...Object.keys(PROBATION_STATUSES).map((status) => ({
        value: status,
        label: t(`probation.status.${status}`),
      })),
    ],
    [t],
  )

  const columns = useMemo<Array<DataColumn<ProbationItem>>>(
    () => [
      helper.accessor('employeeName', {
        header: t('employee.fullName'),
        cell: ({ row }) => {
          const item = row.original

          return (
            <div className="flex min-w-0 items-center gap-3">
              {/*
                Rasm shu yerda ham ko'rsatiladi: HR xodimni ismidan emas,
                yuzidan taniydi va ro'yxatdan ro'yxatga o'tganda bir xil
                ko'rinish saqlanishi kerak.
              */}
              <EmployeePhoto
                employeeId={item.employeeId}
                photoKey={item.photoKey}
                name={item.employeeName}
                size="md"
              />

              <div className="min-w-0">
                <p className="truncate-1 font-medium">{item.employeeName}</p>
                {item.position !== null && (
                  <p className="truncate-1 mt-0.5 text-caption1 text-[var(--fg-tertiary)]">
                    {item.position}
                  </p>
                )}
              </div>
            </div>
          )
        },
      }),

      helper.accessor('department', {
        header: t('employee.department'),
        enableSorting: false,
        meta: { width: '200px', hideOnMobile: true },
        cell: ({ getValue }) => <Muted value={getValue()} />,
      }),

      helper.accessor('startDate', {
        header: t('probation.startDate'),
        meta: { width: '130px', hideOnMobile: true },
        cell: ({ getValue }) => <DateText value={getValue()} />,
      }),

      helper.accessor('endDate', {
        header: t('probation.endDate'),
        meta: { width: '130px' },
        cell: ({ getValue }) => <DateText value={getValue()} />,
      }),

      helper.accessor('daysLeft', {
        header: t('probation.daysLeft'),
        meta: { width: '150px' },
        cell: ({ row }) => <DaysLeft item={row.original} />,
      }),

      helper.accessor('status', {
        header: t('employee.status'),
        meta: { width: '150px' },
        cell: ({ row }) => (
          <Badge tone={STATUS_TONE[row.original.status]} size="sm">
            {t(`probation.status.${row.original.status}`)}
          </Badge>
        ),
      }),

      helper.display({
        id: 'actions',
        header: '',
        meta: { width: '190px', align: 'end' },
        cell: ({ row }) => {
          const item = row.original

          // Yakunlangan muddatni qayta yakunlab yoki uzaytirib bo'lmaydi —
          // domen ham buni rad etadi. Tugmani ko'rsatib, keyin xato
          // chiqarish foydalanuvchini aldash bo'lardi.
          if (!canManage || item.status !== 'inProgress') return null

          return (
            <span className="flex justify-end gap-1.5">
              <Button size="sm" variant="secondary" onClick={() => setExtending(item)}>
                {t('probation.extend')}
              </Button>
              <Button size="sm" onClick={() => setResolving(item)}>
                {t('probation.resolve')}
              </Button>
            </span>
          )
        },
      }),
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-5">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={Timer} tone="orange" />
            {t('probation.title')}
          </span>
        }
        subtitle={list.isSuccess ? t('probation.subtitle', { n: num(rows.length) }) : undefined}
      />

      {soonCount > 0 && (
        <Alert tone="warning" title={t('probation.endingSoonTitle', { n: num(soonCount) })}>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <span>{t('probation.endingSoonText', { days: WARNING_DAYS })}</span>

            {filters.status !== 'inProgress' && (
              <Button
                size="sm"
                variant="secondary"
                onClick={() => setFilters({ status: 'inProgress' })}
              >
                {t('probation.showActive')}
              </Button>
            )}
          </div>
        </Alert>
      )}

      <Card>
        <div className="border-b border-[var(--border)] p-4">
          <Select
            options={statusOptions}
            value={filters.status}
            onChange={(event) =>
              setFilters({ status: event.target.value as ProbationFilters['status'] })
            }
            className="max-w-[260px]"
          />
        </div>

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          loading={list.isPending}
          rowLabel={(row) => row.employeeName}
          empty={
            <EmptyState
              icon={CalendarClock}
              title={t('probation.emptyTitle')}
              description={t('probation.emptyText')}
            />
          }
        />
      </Card>

      <ResolveDialog item={resolving} onClose={() => setResolving(null)} />
      <ExtendDialog item={extending} onClose={() => setExtending(null)} />
    </div>
  )
}

function Muted({ value }: { value: string | null }) {
  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="truncate-1 text-[var(--fg-secondary)]">{value}</span>
}

function DateText({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}

/**
 * Qolgan kun — ekrandagi eng muhim ustun.
 *
 * Uch holat uch xil ko'rinadi va rang yagona signal EMAS: matn ham
 * o'zgaradi. Rangni ajratmaydigan foydalanuvchi ham «muddat o'tdi» ni
 * o'qiy olishi kerak.
 */
function DaysLeft({ item }: { item: ProbationItem }) {
  const { t, num } = useLocale()

  if (item.status !== 'inProgress') {
    return <span className="text-[var(--fg-tertiary)]">—</span>
  }

  if (item.daysLeft < 0) {
    return (
      <span className="flex items-center gap-1.5 font-medium text-ios-red">
        <CircleAlert className="size-4 shrink-0" aria-hidden />
        {t('probation.overdue', { n: num(Math.abs(item.daysLeft)) })}
      </span>
    )
  }

  return (
    <span
      className={
        item.needsWarning
          ? 'flex items-center gap-1.5 font-medium text-ios-orange'
          : 'tabular text-[var(--fg-secondary)]'
      }
    >
      {item.needsWarning && <CircleAlert className="size-4 shrink-0" aria-hidden />}
      {t('probation.daysValue', { n: num(item.daysLeft) })}
    </span>
  )
}
