import { getRouteApi } from '@tanstack/react-router'
import { MessageSquareQuote, Pencil, Plus, Search, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import { toSearchParam, useSearchDraft } from '@/shared/lib/list-search'
import {
  Badge,
  Button,
  Card,
  DataTable,
  DateField,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type { ExitInterview } from '../api/exit-interviews-api'
import { useExitInterviews } from '../api/exit-interviews-queries'
import { FEEDBACK_MANAGE, FEEDBACK_VIEW, RECOMMENDATION_LOOK } from '../lib/feedback-presentation'
import { ExitInterviewDeleteDialog } from './exit-interview-delete-dialog'
import { ExitInterviewFormDialog } from './exit-interview-form-dialog'

const helper = dataColumnHelper<ExitInterview>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: ExitInterview[] = []

interface Filters {
  from: string
  to: string
}

const DEFAULT_FILTERS: Filters = { from: '', to: '' }

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/qayta-aloqa')

/**
 * Qayta aloqa (exit interview) jurnali — 91.16 o'rniga.
 *
 * `incidents-page.tsx` bilan bir xil naqsh va bir xil soddalashtirish
 * (filtr manzilda saqlanmaydi — atigi ikkita filtr, kamdan-kam ishlatiladi).
 */
export function ExitInterviewsPage() {
  const { t, num, date } = useLocale()
  const { hasPermission } = useAuth()

  const canView = hasPermission(FEEDBACK_VIEW)
  const canManage = hasPermission(FEEDBACK_MANAGE)

  const searchParams = route.useSearch()
  const navigate = route.useNavigate()

  const search = searchParams.q ?? ''

  const [filters, setFilters] = useState<Filters>(DEFAULT_FILTERS)
  const [editing, setEditing] = useState<ExitInterview | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [deleting, setDeleting] = useState<ExitInterview | null>(null)

  /*
   * Qidiruv har harfda so'rov yubormaydi (350 ms) va matn manzilga
   * yoziladi. `replace` — qidiruvni aniqlashtirish alohida tarix qadami
   * emas: aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
   */
  const [draft, setDraft] = useSearchDraft(search, (value) => {
    void navigate({ search: { q: toSearchParam(value) }, replace: true })
  })

  // `canView` yo'q bo'lsa ham so'rov chaqiriladi — `incidents-page.tsx`
  // dagi bilan bir xil naqsh (`users-page.tsx`dan meros).
  const list = useExitInterviews({
    from: filters.from === '' ? undefined : filters.from,
    to: filters.to === '' ? undefined : filters.to,
    search: search === '' ? undefined : search,
  })
  const rows = canView ? (list.data ?? EMPTY) : EMPTY

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (item: ExitInterview) => {
    setEditing(item)
    setFormOpen(true)
  }

  const columns = useMemo<Array<DataColumn<ExitInterview>>>(() => {
    const definitions: Array<DataColumn<ExitInterview>> = [
      helper.accessor('employeeName', {
        header: t('employee.fullName'),
        // `incidents-page.tsx` dagi bilan bir xil hisob — uch ro'yxatda
        // bir xil odam bir xil kenglikda ko'rinsin.
        meta: { width: '300px' },
        cell: ({ row }) => (
          <div className="flex min-w-0 items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId ?? ''}
              photoKey={row.original.photoKey}
              name={row.original.employeeName ?? ''}
              size="lg"
            />
            <p className="truncate-1 font-medium">{row.original.employeeName ?? '—'}</p>
          </div>
        ),
      }),

      helper.display({
        id: 'workplace',
        header: t('feedback.workplace'),
        enableSorting: false,
        // 200px − katak to'ldirishi (32px) = 168px: bo'lim nomi bitta
        // qatorda (`truncate-1`), ostida lavozim — ikkalasi ham odatiy
        // uzunlikda (`positions.department` ustuni bilan bir xil taxmin,
        // `catalog-page.tsx`).
        meta: { width: '200px', hideOnMobile: true },
        cell: ({ row }) => (
          <div className="min-w-0">
            <p className="truncate-1">{row.original.departmentName ?? row.original.legalEntityName ?? '—'}</p>
            {row.original.positionName !== null && (
              <p className="truncate-1 text-caption1 text-[var(--fg-tertiary)]">
                {row.original.positionName}
              </p>
            )}
          </div>
        ),
      }),

      helper.accessor('terminationDate', {
        header: t('feedback.terminationDate'),
        // «25.08.2026» — 10 belgi, tabular. `incidents-page.tsx` dagi sana
        // ustuni bilan bir xil hisob: 120px.
        meta: { width: '120px', hideOnMobile: true },
        cell: ({ getValue }) => {
          const value = getValue()
          if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
          return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
        },
      }),

      helper.accessor('interviewDate', {
        header: t('feedback.interviewDate'),
        meta: { width: '120px' },
        cell: ({ getValue }) => <span className="tabular text-[var(--fg-secondary)]">{date(getValue())}</span>,
      }),

      helper.accessor('wouldRecommend', {
        header: t('feedback.wouldRecommend'),
        // Belgi — eng uzun yorliq «Bilmayman» (9 belgi), `incidents-page.tsx`
        // dagi daraja ustuni bilan bir xil hisob: 150px.
        meta: { width: '150px' },
        cell: ({ row }) => {
          const value = row.original.wouldRecommend
          if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>

          const look = RECOMMENDATION_LOOK[value]
          const Icon = look.icon
          return (
            <Badge tone={look.tone} size="sm">
              <Icon className="size-3" strokeWidth={2.5} />
              {t(look.labelKey)}
            </Badge>
          )
        },
      }),

      helper.accessor('reason', {
        header: t('feedback.reason'),
        enableSorting: false,
        // Ortgan joyni shu ustun oladi — sabab erkin matn, ko'pincha bir
        // necha jumla (`flags.reason` ustuni bilan bir xil naqsh,
        // `flag-list-page.tsx`). 220px pastki chegara.
        meta: { minWidth: '220px' },
        cell: ({ getValue }) => (
          <span className="truncate-2 text-[var(--fg-secondary)]" title={getValue()}>
            {getValue()}
          </span>
        ),
      }),
    ]

    if (canManage) {
      definitions.push(
        helper.display({
          id: 'actions',
          header: '',
          // Ikkita ikonkali tugma — `users-page.tsx` dagi bilan bir xil
          // hisob: 110px.
          meta: { align: 'end', width: '110px' },
          cell: ({ row }) => {
            const item = row.original
            return (
              <span className="flex justify-end gap-1.5">
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  title={t('common.edit')}
                  aria-label={t('common.edit')}
                  onClick={(event) => {
                    event.stopPropagation()
                    openEdit(item)
                  }}
                >
                  <Pencil />
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  title={t('common.delete')}
                  aria-label={t('common.delete')}
                  onClick={(event) => {
                    event.stopPropagation()
                    setDeleting(item)
                  }}
                >
                  <Trash2 className="text-[var(--danger-text)]" />
                </Button>
              </span>
            )
          },
        }),
      )
    }

    return definitions
  }, [canManage, t, date])

  if (!canView) {
    return (
      <div className="space-y-6">
        <PageHeader
          title={
            <span className="flex items-center gap-3">
              <IconTile icon={MessageSquareQuote} tone="purple" size="lg" />
              {t('nav.feedback')}
            </span>
          }
        />
        <Card>
          <EmptyState
            icon={MessageSquareQuote}
            title={t('feedback.noAccessTitle')}
            description={t('feedback.noAccessText')}
          />
        </Card>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={MessageSquareQuote} tone="purple" size="lg" />
            {t('nav.feedback')}
          </span>
        }
        subtitle={list.isPending ? t('common.loading') : t('feedback.subtitle', { n: num(rows.length) })}
        actions={
          canManage ? (
            <Button variant="create" onClick={openCreate}>
              <Plus />
              {t('feedback.add')}
            </Button>
          ) : undefined
        }
      />

      <Card className="overflow-hidden">
        {/*
          Qidiruv sana oralig'idan YUQORIDA va butun kenglikda: HR bu
          jurnalga «falonchi nima degan edi» degan savol bilan keladi,
          sana oralig'i esa kamdan-kam ishlatiladi.
        */}
        <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
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

        <div className="grid gap-3 border-b border-[var(--border)] px-4 py-3.5 sm:grid-cols-2 sm:px-5">
          <DateField
            label={t('common.dateFrom')}
            value={filters.from}
            onChange={(value) => setFilters({ ...filters, from: value })}
          />
          <DateField
            label={t('common.dateTo')}
            value={filters.to}
            onChange={(value) => setFilters({ ...filters, to: value })}
          />
        </div>

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(item) => item.id}
          loading={list.isPending}
          rowLabel={(item) => item.employeeName ?? ''}
          empty={
            <EmptyState
              icon={MessageSquareQuote}
              title={t('feedback.emptyTitle')}
              description={t('feedback.emptyText')}
              action={
                canManage ? (
                  <Button variant="create" onClick={openCreate}>
                    <Plus />
                    {t('feedback.add')}
                  </Button>
                ) : undefined
              }
            />
          }
        />
      </Card>

      <ExitInterviewFormDialog open={formOpen} onClose={() => setFormOpen(false)} item={editing} />
      <ExitInterviewDeleteDialog item={deleting} onClose={() => setDeleting(null)} />
    </div>
  )
}
