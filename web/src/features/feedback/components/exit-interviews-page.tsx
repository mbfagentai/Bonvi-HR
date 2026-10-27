import { getRouteApi } from '@tanstack/react-router'
import { MessageSquareQuote, Pencil, Plus, Search, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { REPORTS_EXPORT } from '@/features/employees/lib/employee-presentation'
import { useListExport } from '@/features/reports/api/list-export'
import { ListExportButton, ListExportError } from '@/features/reports/components/list-export'
import { useLocale } from '@/shared/i18n/use-locale'
import { isDateRangeInverted, toSearchParam, useSearchDraft } from '@/shared/lib/list-search'
import {
  Badge,
  Button,
  Card,
  DataTable,
  DateRangeField,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import {
  exitInterviewListParams,
  type ExitInterview,
  type ExitInterviewFilters,
} from '../api/exit-interviews-api'
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
  // Eksport — jurnalni ko'rish VA `reports.export` (SPEC-excel-eksport §1)
  const canExport = canView && hasPermission(REPORTS_EXPORT)

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
  /*
    Filtr — ro'yxat ham, Excel eksporti ham AYNAN shundan (ro'yxat
    sahifalanmaydi, ya'ni fayl ekrandagi qatorlarning o'zi).
  */
  const listFilters: ExitInterviewFilters = {
    from: filters.from === '' ? undefined : filters.from,
    to: filters.to === '' ? undefined : filters.to,
    search: search === '' ? undefined : search,
  }

  const list = useExitInterviews(listFilters)

  // Xato mutatsiyada qoladi — keyingi bosish uni o'zi tozalaydi (`employees-page.tsx`)
  const exportList = useListExport()
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
        /*
          `incidents-page.tsx` dagi bilan bir xil: rasm va to'liq ism BIR
          QATORDA (egasi 2026-10-22 — jadval qatori ikkiga bo'linmaydi).
          300px — pastki chegara, uzun ismda ustun o'zi kengayadi.
        */
        meta: { width: '300px' },
        cell: ({ row }) => (
          <div className="flex items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId ?? ''}
              photoKey={row.original.photoKey}
              name={row.original.employeeName ?? ''}
              size="md"
            />
            <span className="font-medium">{row.original.employeeName ?? '—'}</span>
          </div>
        ),
      }),

      /*
        Ish joyi — bo'lim va lavozim endi IKKI ustunda (ilgari bo'lim
        ostiga lavozim tushardi). Xodimlar ro'yxatidagi tartib bilan bir xil.
      */
      helper.display({
        id: 'department',
        header: t('employee.department'),
        enableSorting: false,
        // Pastki chegara 160px, yuqorisi 260px (`maxWidth` — uzunrog'i «…», to'liq nomi `title` da)
        meta: { width: '160px', maxWidth: '260px', hideOnMobile: true },
        cell: ({ row }) => {
          const value = row.original.departmentName ?? row.original.legalEntityName
          return value === null ? (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ) : (
            <span title={value} className="text-[var(--fg-secondary)]">{value}</span>
          )
        },
      }),

      helper.display({
        id: 'position',
        header: t('employee.position'),
        enableSorting: false,
        meta: { width: '160px', maxWidth: '260px', hideOnMobile: true },
        cell: ({ row }) =>
          row.original.positionName === null ? (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ) : (
            <span title={row.original.positionName} className="text-[var(--fg-secondary)]">
              {row.original.positionName}
            </span>
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
        // `flag-list-page.tsx`). 180px pastki chegara. Bir qatorda, ustunga
        // sig'magani kesiladi — to'lig'i `title` da va tahrirlash oynasida.
        // `absolute` qatlam — matn ustunning eng kichik kengligiga qo'shilmasin
        // (`audit-list.tsx` dagi o'zgarishlar ustuni bilan bir xil naqsh).
        meta: { minWidth: '180px' },
        cell: ({ getValue }) => (
          <span className="relative block h-5" title={getValue()}>
            <span className="absolute inset-0 truncate leading-5 text-[var(--fg-secondary)]">{getValue()}</span>
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

  /*
    Ro'yxat ramkasi (`data-list-frame`, `app-shell.tsx`): sahifa ekranning
    butun eniga yoyiladi, qatorlar esa kartaning ichida aylanadi — jadval
    bir qatorli bo'lgach (egasi 2026-10-22) u 1400px ga sig'masdi.
  */
  return (
    <div data-list-frame className="space-y-6 frame:flex frame:h-full frame:flex-col">
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

      <Card className="overflow-hidden frame:flex frame:flex-1 frame:flex-col">
        {/*
          Qidiruv va sana oralig'i BITTA qatorda (ilgari ikki qator — jadvaldan
          joy olardi). Qidiruv birinchi va eng keng: HR bu jurnalga «falonchi
          nima degan edi» degan savol bilan keladi, sana oralig'i esa
          kamdan-kam ishlatiladi.
        */}
        <div className="flex flex-wrap items-end gap-3 border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
          <div className="min-w-[240px] flex-1">
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
          <DateRangeField
            label={t('dateRange.date')}
            fromLabel={t('common.dateFrom')}
            toLabel={t('common.dateTo')}
            from={filters.from}
            to={filters.to}
            onFromChange={(value) => setFilters({ ...filters, from: value })}
            onToChange={(value) => setFilters({ ...filters, to: value })}
            className="w-full sm:w-[340px]"
          />

          {/* Eksport — filtr qatorining oxirida: fayl aynan shu qidiruv va oraliqqa tushadi */}
          {canExport && (
            <ListExportButton
              onClick={() =>
                exportList.mutate({
                  path: '/api/exit-interviews/export',
                  params: exitInterviewListParams(listFilters),
                  slug: 'qayta_aloqa',
                })
              }
              pending={exportList.isPending}
              // Teskari oraliqqa hech narsa mos kelmaydi — bo'sh fayl yuklashning ma'nosi yo'q
              disabled={isDateRangeInverted(filters)}
            />
          )}
        </div>

        <ListExportError error={exportList.error} />

        <DataTable
          fill
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
