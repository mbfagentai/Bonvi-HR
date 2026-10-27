import { ChevronRight, MapPin, Plus, Search, SearchX } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { matchesSearch } from '@/shared/lib/search-fold'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  Input,
  PageHeader,
  Switch,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import {
  AttendanceLoadError,
  AttendanceNoAccess,
  AttendancePageTitle,
} from '../../components/attendance-manage-states'
import { ATTENDANCE_MANAGE, ATTENDANCE_VIEW } from '../../lib/attendance-presentation'
import { WorkSiteShape, type WorkSite } from '../api/work-sites-api'
import { useWorkSites } from '../api/work-sites-queries'
import { WorkSiteFormDialog } from './work-site-form-dialog'

const helper = dataColumnHelper<WorkSite>()

/** Module level — a new array per render would rebuild the table model. */
const EMPTY: WorkSite[] = []

/**
 * Verifikatsiya → Joylashuvlar (SPEC-joylashuvlar §D): the places where a
 * phone check-in is accepted.
 *
 * Reading — `attendance.view`; create / edit / (de)activate —
 * `attendance.manage`. Without `manage` the rows still open, but the dialog
 * is read-only (the map is the whole point of the page).
 *
 * The list comes whole (a handful of sites) and «Faolsizlarni ko'rsatish»
 * filters on the client — so the page can warn that there is NO active site
 * even while inactive ones are hidden: in that state every phone check-in is
 * rejected (`davomat.obyektdan_uzoq`).
 */
export function WorkSitesPage() {
  const { t, num } = useLocale()
  const { hasPermission } = useAuth()

  const canView = hasPermission(ATTENDANCE_VIEW)
  const canManage = hasPermission(ATTENDANCE_MANAGE)

  const sites = useWorkSites(canView)

  const [query, setQuery] = useState('')
  const [showInactive, setShowInactive] = useState(false)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<WorkSite | null>(null)

  const all = sites.data ?? EMPTY
  const activeCount = all.filter((site) => site.isActive).length
  const inactiveCount = all.length - activeCount

  const rows = useMemo(() => {
    const needle = query.trim()
    return all.filter(
      (site) =>
        (showInactive || site.isActive) && (needle === '' || matchesSearch(site.name, needle)),
    )
  }, [all, query, showInactive])

  const openCreate = useCallback(() => {
    setEditing(null)
    setDialogOpen(true)
  }, [])

  const openSite = useCallback((site: WorkSite) => {
    setEditing(site)
    setDialogOpen(true)
  }, [])

  const columns = useMemo<Array<DataColumn<WorkSite>>>(
    () => [
      /*
        ⭐ BIR QATOR (egasi 2026-10-22): izoh nom ostida emas, o'z ustunida;
        uzun manzil va izoh kesiladi, to'liq matn sichqoncha ostida.
      */
      helper.accessor('name', {
        header: t('attendance.workSites.columnName'),
        cell: ({ row }) => (
          <span className={cn('font-medium', !row.original.isActive && 'text-[var(--fg-secondary)]')}>
            {row.original.name}
          </span>
        ),
      }),

      helper.accessor('address', {
        header: t('attendance.workSites.columnAddress'),
        meta: { hideOnMobile: true },
        cell: ({ row }) => <LongText value={row.original.address} />,
      }),

      helper.accessor('note', {
        header: t('employees.note'),
        enableSorting: false,
        meta: { hideOnMobile: true },
        cell: ({ row }) => <LongText value={row.original.note} />,
      }),

      helper.accessor('shape', {
        header: t('attendance.workSites.columnShape'),
        cell: ({ row }) => (
          <span className="tabular text-[var(--fg-secondary)]">
            {row.original.shape === WorkSiteShape.Circle
              ? t('attendance.workSites.shapeCircleSummary', {
                  radius: num(row.original.radiusMeters),
                })
              : t('attendance.workSites.shapePolygonSummary', {
                  buffer: num(row.original.edgeBufferMeters),
                })}
          </span>
        ),
      }),

      helper.accessor('isActive', {
        header: t('attendance.workSites.columnStatus'),
        cell: ({ row }) => (
          <Badge tone={row.original.isActive ? 'success' : 'neutral'} size="sm">
            {row.original.isActive
              ? t('attendance.workSites.statusActive')
              : t('attendance.workSites.statusInactive')}
          </Badge>
        ),
      }),

      helper.display({
        id: 'open',
        header: '',
        meta: { align: 'end', width: '56px' },
        cell: () => (
          <ChevronRight className="ml-auto size-4 text-[var(--fg-tertiary)]" strokeWidth={2.5} />
        ),
      }),
    ],
    [t, num],
  )

  if (!canView) {
    return <AttendanceNoAccess icon={MapPin} title={t('attendance.workSites.title')} />
  }

  const searching = query.trim() !== ''
  const loaded = !sites.isPending && !sites.isError

  const createButton = canManage ? (
    <Button variant="create" onClick={openCreate}>
      <Plus />
      {t('attendance.workSites.create')}
    </Button>
  ) : undefined

  return (
    <div className="space-y-6">
      <PageHeader
        title={<AttendancePageTitle icon={MapPin} title={t('attendance.workSites.title')} />}
        subtitle={
          sites.isPending
            ? t('common.loading')
            : t('attendance.workSites.subtitle', { n: num(all.length), active: num(activeCount) })
        }
        actions={createButton}
      />

      {/*
        Sites exist but none is active — every phone check-in is rejected right
        now. The empty list below covers the «no sites at all» case itself.
      */}
      {loaded && activeCount === 0 && all.length > 0 && (
        <Alert tone="warning" title={t('attendance.workSites.noActiveTitle')}>
          {t('attendance.workSites.noActiveText')}
        </Alert>
      )}

      <Card className="overflow-hidden">
        <div className="flex flex-wrap items-center gap-x-5 gap-y-3 border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
          <div className="min-w-[200px] flex-1">
            <Input
              icon={Search}
              shape="pill"
              type="search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t('attendance.workSites.searchListPlaceholder')}
              aria-label={t('common.search')}
            />
          </div>
          <Switch
            checked={showInactive}
            onChange={setShowInactive}
            label={
              inactiveCount > 0
                ? t('attendance.workSites.showInactiveCount', { n: num(inactiveCount) })
                : t('attendance.workSites.showInactive')
            }
          />
        </div>

        {sites.isError ? (
          <AttendanceLoadError
            error={sites.error}
            onRetry={() => void sites.refetch()}
            retrying={sites.isFetching}
          />
        ) : (
          <DataTable
            columns={columns}
            data={rows}
            getRowId={(site) => site.id}
            loading={sites.isPending}
            onRowClick={openSite}
            rowLabel={(site) => site.name}
            empty={
              searching ? (
                <EmptyState
                  icon={SearchX}
                  title={t('attendance.workSites.notFoundTitle')}
                  description={t('attendance.workSites.notFoundText', { query: query.trim() })}
                />
              ) : all.length === 0 ? (
                <EmptyState
                  icon={MapPin}
                  title={t('attendance.workSites.emptyTitle')}
                  description={t('attendance.workSites.emptyText')}
                  action={createButton}
                />
              ) : (
                <EmptyState
                  icon={MapPin}
                  title={t('attendance.workSites.emptyActiveTitle')}
                  description={t('attendance.workSites.emptyActiveText')}
                  action={
                    <Button type="button" onClick={() => setShowInactive(true)}>
                      {t('attendance.workSites.showInactive')}
                    </Button>
                  }
                />
              )
            }
          />
        )}

        {loaded && rows.length > 0 && (
          <p className="border-t border-[var(--border)] px-5 py-3 text-caption1 text-[var(--fg-tertiary)]">
            {t('attendance.workSites.total', { n: num(rows.length) })}
          </p>
        )}
      </Card>

      <WorkSiteFormDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        site={editing}
        readOnly={!canManage}
        activeCount={activeCount}
      />
    </div>
  )
}

/** Erkin matn (manzil, izoh) — bir qatorda, uzun bo'lsa kesiladi, to'liq matn `title` da. */
function LongText({ value }: { value: string | null }) {
  if (value === null || value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return (
    <span className="block max-w-[360px] truncate text-[var(--fg-secondary)]" title={value}>
      {value}
    </span>
  )
}
