import { getRouteApi } from '@tanstack/react-router'
import { ClipboardCheck, HeartPulse, Pencil, Plus, Search, Trash2 } from 'lucide-react'
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
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Select,
  TextField,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import { INCIDENT_SEVERITIES, type Incident, type IncidentSeverityValue } from '../api/incidents-api'
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
  const list = useIncidents({
    from: filters.from === '' ? undefined : filters.from,
    to: filters.to === '' ? undefined : filters.to,
    severity: filters.severity === '' ? undefined : filters.severity,
    search: search === '' ? undefined : search,
  })
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
          `flag-list-page.tsx` dagi bilan bir xil hisob va sabab: 300px −
          katak to'ldirishi (32px) − rasm (48px) − oraliq (12px) = 208px sof
          matnga. Uch ro'yxatda (belgilar, sinov, bu yerda) bir xil odam bir
          xil kenglikda ko'rinsin — HR ko'zi shu o'lchamga o'rgangan.
        */
        meta: { width: '300px' },
        cell: ({ row }) => (
          <div className="flex min-w-0 items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId}
              photoKey={row.original.photoKey}
              name={row.original.employeeName ?? ''}
              size="lg"
            />
            <p className="truncate-1 font-medium">{row.original.employeeName}</p>
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
        // xil naqsh). 220px — pastki chegara, odatiy joy nomi (sex, ombor
        // nomi) bir qatorga sig'adi.
        meta: { minWidth: '220px' },
        cell: ({ getValue }) => (
          <span className="truncate-1 block text-[var(--fg-secondary)]" title={getValue()}>
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

        <div className="grid gap-3 border-b border-[var(--border)] px-4 py-3.5 sm:grid-cols-3 sm:px-5">
          <TextField
            label={t('common.dateFrom')}
            type="date"
            value={filters.from}
            onChange={(event) => setFilters({ ...filters, from: event.target.value })}
          />
          <TextField
            label={t('common.dateTo')}
            type="date"
            value={filters.to}
            onChange={(event) => setFilters({ ...filters, to: event.target.value })}
          />
          <Select
            label={t('safety.severity')}
            options={severityOptions}
            value={filters.severity}
            onChange={(event) =>
              setFilters({ ...filters, severity: event.target.value as IncidentSeverityValue | '' })
            }
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
