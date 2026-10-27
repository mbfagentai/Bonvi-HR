import { getRouteApi } from '@tanstack/react-router'
import { ClipboardCheck, HeartPulse, Pencil, Plus, Search, Trash2 } from 'lucide-react'
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
  Select,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import {
  INCIDENT_SEVERITIES,
  incidentListParams,
  type Incident,
  type IncidentFilters,
  type IncidentSeverityValue,
} from '../api/incidents-api'
import { useIncidents } from '../api/incidents-queries'
import { SAFETY_MANAGE, SAFETY_VIEW, SEVERITY_LOOK } from '../lib/safety-presentation'
import { IncidentConcludeDialog } from './incident-conclude-dialog'
import { IncidentDeleteDialog } from './incident-delete-dialog'
import { IncidentFormDialog } from './incident-form-dialog'

const helper = dataColumnHelper<Incident>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: Incident[] = []

interface Filters {
  from: string
  to: string
  severity: IncidentSeverityValue | ''
}

const DEFAULT_FILTERS: Filters = { from: '', to: '', severity: '' }

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/hodisalar')

/**
 * Baxtsiz hodisalar jurnali.
 *
 * ALOHIDA sahifa, xodim kartasi ostida emas: bu jurnal ko'rinishi HR ga
 * «shu oyda nechta hodisa bo'ldi, qay darajada» degan savolga xodimlar
 * ro'yxatini aylanib chiqmasdan javob beradi. Xodim profilidan qidirish
 * esa aksincha savolga («shu odam bilan biror narsa bo'lganmi?») javob
 * berardi — ikkalasi ham kerak, lekin bu ISH faqat jurnal tomonini quradi.
 *
 * Filtr URL manzilida saqlanmaydi (`buyruqlar.tsx` dagi buyruqlar
 * ro'yxatidan farqli) — bu yerda atigi ikki filtr bor (sana oralig'i,
 * daraja) va ular kamdan-kam ishlatiladi, manzil bilan bog'lashning
 * foydasi xarajatini qoplamaydi (vaqt cheklovi bilan bog'liq soddalashtirish).
 */
export function IncidentsPage() {
  const { t, num, date } = useLocale()
  const { hasPermission } = useAuth()

  const canView = hasPermission(SAFETY_VIEW)
  const canManage = hasPermission(SAFETY_MANAGE)
  // Eksport — jurnalni ko'rish VA `reports.export` (SPEC-excel-eksport §1)
  const canExport = canView && hasPermission(REPORTS_EXPORT)

  const searchParams = route.useSearch()
  const navigate = route.useNavigate()

  const search = searchParams.q ?? ''

  /*
   * Qidiruv har harfda so'rov yubormaydi (350 ms) va matn manzilga
   * yoziladi. `replace` — qidiruvni aniqlashtirish alohida tarix qadami
   * emas: aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
   */
  const [draft, setDraft] = useSearchDraft(search, (value) => {
    void navigate({ search: { q: toSearchParam(value) }, replace: true })
  })

  const [filters, setFilters] = useState<Filters>(DEFAULT_FILTERS)
  const [editing, setEditing] = useState<Incident | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [concluding, setConcluding] = useState<Incident | null>(null)
  const [deleting, setDeleting] = useState<Incident | null>(null)

  /*
   * `canView` yo'q bo'lsa ham so'rov chaqiriladi — `users-page.tsx` dagi
   * `useUsers()` bilan bir xil naqsh: server baribir 403 qaytaradi, bu
   * yerda faqat natija ko'rsatilmaydi (pastdagi `!canView` bo'limi).
   */
  /*
    Filtr — ro'yxat ham, Excel eksporti ham AYNAN shundan (ro'yxat
    sahifalanmaydi, ya'ni fayl ekrandagi qatorlarning o'zi).
  */
  const listFilters: IncidentFilters = {
    from: filters.from === '' ? undefined : filters.from,
    to: filters.to === '' ? undefined : filters.to,
    severity: filters.severity === '' ? undefined : filters.severity,
    search: search === '' ? undefined : search,
  }

  const list = useIncidents(listFilters)

  // Xato mutatsiyada qoladi — keyingi bosish uni o'zi tozalaydi (`employees-page.tsx`)
  const exportList = useListExport()
  const rows = canView ? (list.data ?? EMPTY) : EMPTY

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (item: Incident) => {
    setEditing(item)
    setFormOpen(true)
  }

  const columns = useMemo<Array<DataColumn<Incident>>>(() => {
    const definitions: Array<DataColumn<Incident>> = [
      helper.accessor('employeeName', {
        header: t('employee.fullName'),
        /*
          Rasm va to'liq ism BIR QATORDA (egasi 2026-10-22 — jadval qatori
          ikkiga bo'linmaydi). 300px — pastki chegara, uch ro'yxatda
          (belgilar, sinov, bu yerda) bir xil; uzun ismda ustun o'zi
          kengayadi — ism kesilmaydi.
        */
        meta: { width: '300px' },
        cell: ({ row }) => (
          <div className="flex items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId}
              photoKey={row.original.photoKey}
              name={row.original.employeeName ?? ''}
              size="md"
            />
            <span className="font-medium">{row.original.employeeName}</span>
          </div>
        ),
      }),

      helper.accessor('incidentDate', {
        header: t('safety.date'),
        // «25.08.2026» — 10 belgi, tabular. 10 × 8.5px ≈ 85px + katak
        // to'ldirishi (32px) ≈ 117px, kichik zaxira bilan 120px.
        meta: { width: '120px' },
        cell: ({ getValue }) => <span className="tabular text-[var(--fg-secondary)]">{date(getValue())}</span>,
      }),

      helper.accessor('severity', {
        header: t('safety.severity'),
        // Belgi (ikon + matn) — eng uzun yorliq «Halokatli» (9 belgi):
        // ikon (14px) + oraliq (6px) + matn (~65px) + yorliq ichki
        // bo'shlig'i (px-2.5×2=20px) ≈ 105px + katak to'ldirishi (32px)
        // ≈ 137px, kichik zaxira bilan 150px.
        meta: { width: '150px' },
        cell: ({ row }) => {
          const look = SEVERITY_LOOK[row.original.severity]
          const Icon = look.icon
          return (
            <Badge tone={look.tone} size="sm">
              <Icon className="size-3" strokeWidth={2.5} />
              {t(look.labelKey)}
            </Badge>
          )
        },
      }),

      helper.accessor('location', {
        header: t('safety.location'),
        enableSorting: false,
        // Ortgan joyni shu ustun oladi — joy nomi erkin matn, uzunligi
        // oldindan noma'lum (`catalog-page.tsx` dagi «nom» ustuni bilan bir
        // xil naqsh). 220px — pastki chegara; bir qatorda, 360px dan uzuni
        // kesiladi — to'lig'i `title` da.
        meta: { minWidth: '220px' },
        cell: ({ getValue }) => (
          <span className="block max-w-[360px] truncate text-[var(--fg-secondary)]" title={getValue()}>
            {getValue()}
          </span>
        ),
      }),

      helper.display({
        id: 'conclusion',
        header: t('safety.conclusionColumn'),
        // «Yakunlangan» (11 belgi) yorlig'i — 11×7.5px(caption1)≈83px +
        // yorliq ichki bo'shlig'i (20px) ≈ 103px + katak to'ldirishi (32px)
        // ≈ 135px, zaxira bilan 150px.
        meta: { width: '150px', hideOnMobile: true },
        cell: ({ row }) => {
          const concluded = row.original.commissionConclusion !== null
          return (
            <Badge
              tone={concluded ? 'success' : 'neutral'}
              size="sm"
              title={
                concluded && row.original.concludedAt !== null
                  ? date(row.original.concludedAt)
                  : undefined
              }
            >
              {concluded ? t('safety.concluded') : t('safety.pending')}
            </Badge>
          )
        },
      }),
    ]

    if (canManage) {
      definitions.push(
        helper.display({
          id: 'actions',
          header: '',
          /*
            Uchta ikonkali tugma (`icon-sm` = 32px) + ikkita oraliq
            (gap-1.5 = 6px) = 108px + katak to'ldirishi (32px) = 140px,
            zaxira bilan 150px (`users-page.tsx` dagi ikki tugmali hisobning
            davomi). Xulosa allaqachon yozilgan qatorda birinchi tugma
            chizilmaydi — ustun kengligi o'zgarmaydi, faqat bo'sh joy qoladi.
          */
          meta: { align: 'end', width: '150px' },
          cell: ({ row }) => {
            const item = row.original
            const concluded = item.commissionConclusion !== null

            return (
              <span className="flex justify-end gap-1.5">
                {!concluded && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    title={t('safety.conclude')}
                    aria-label={t('safety.conclude')}
                    onClick={(event) => {
                      event.stopPropagation()
                      setConcluding(item)
                    }}
                  >
                    <ClipboardCheck />
                  </Button>
                )}
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
              <IconTile icon={HeartPulse} tone="red" size="lg" />
              {t('nav.incidents')}
            </span>
          }
        />
        <Card>
          <EmptyState
            icon={HeartPulse}
            title={t('safety.noAccessTitle')}
            description={t('safety.noAccessText')}
          />
        </Card>
      </div>
    )
  }

  const severityOptions = [
    { value: '', label: t('safety.anySeverity') },
    ...INCIDENT_SEVERITIES.map((value) => ({ value, label: t(SEVERITY_LOOK[value].labelKey) })),
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={HeartPulse} tone="red" size="lg" />
            {t('nav.incidents')}
          </span>
        }
        subtitle={list.isPending ? t('common.loading') : t('safety.subtitle', { n: num(rows.length) })}
        actions={
          canManage ? (
            <Button variant="create" onClick={openCreate}>
              <Plus />
              {t('safety.add')}
            </Button>
          ) : undefined
        }
      />

      <Card className="overflow-hidden">
        {/*
          Qidiruv sana va daraja filtrlaridan YUQORIDA va butun kenglikda:
          jurnalga «falonchi bilan nima bo'lgan edi» degan savol bilan
          kelinadi, qolgan ikkitasi esa kamdan-kam ishlatiladi.
        */}
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

          {/*
            Eksport — yuqori filtr qatorida, qidiruv yonida (xodimlar ro'yxatidagi
            joy): fayl aynan shu qidiruv, sana va darajaga tushadi.
          */}
          {canExport && (
            <ListExportButton
              onClick={() =>
                exportList.mutate({
                  path: '/api/safety/incidents/export',
                  params: incidentListParams(listFilters),
                  slug: 'baxtsiz_hodisalar',
                })
              }
              pending={exportList.isPending}
              // Teskari oraliqqa hech narsa mos kelmaydi — bo'sh fayl yuklashning ma'nosi yo'q
              disabled={isDateRangeInverted(filters)}
            />
          )}
        </div>

        <div className="grid gap-3 border-b border-[var(--border)] px-4 py-3.5 sm:grid-cols-3 sm:px-5">
          <DateRangeField
            label={t('dateRange.date')}
            fromLabel={t('common.dateFrom')}
            toLabel={t('common.dateTo')}
            from={filters.from}
            to={filters.to}
            onFromChange={(value) => setFilters({ ...filters, from: value })}
            onToChange={(value) => setFilters({ ...filters, to: value })}
          />
          <Select
            size="sm"
            label={t('safety.severity')}
            options={severityOptions}
            value={filters.severity}
            onChange={(event) =>
              setFilters({ ...filters, severity: event.target.value as IncidentSeverityValue | '' })
            }
          />
        </div>

        <ListExportError error={exportList.error} />

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(item) => item.id}
          loading={list.isPending}
          rowLabel={(item) => item.employeeName ?? ''}
          empty={
            <EmptyState
              icon={HeartPulse}
              title={t('safety.emptyTitle')}
              description={t('safety.emptyText')}
              action={
                canManage ? (
                  <Button variant="create" onClick={openCreate}>
                    <Plus />
                    {t('safety.add')}
                  </Button>
                ) : undefined
              }
            />
          }
        />
      </Card>

      <IncidentFormDialog open={formOpen} onClose={() => setFormOpen(false)} item={editing} />
      <IncidentConcludeDialog item={concluding} onClose={() => setConcluding(null)} />
      <IncidentDeleteDialog item={deleting} onClose={() => setDeleting(null)} />
    </div>
  )
}
