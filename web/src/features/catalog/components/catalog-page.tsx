import { ChevronRight, Plus, SearchX } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import { useCatalogList, useToggleCatalogItem } from '../api/catalog-queries'
import type { CatalogItem } from '../api/catalog-api'
import { catalogErrorCode } from '../lib/catalog-error'
import { CATALOG_MANAGE_PERMISSION, type CatalogKindMeta } from '../lib/catalog-kinds'
import { CatalogFormDialog } from './catalog-form-dialog'
import { CatalogStatus } from './catalog-status'
import { CatalogToolbar } from './catalog-toolbar'

const helper = dataColumnHelper<CatalogItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: CatalogItem[] = []

/**
 * Oddiy ma'lumotnoma sahifasi.
 *
 * Oltita ma'lumotnoma (yuridik shaxslar, yo'nalishlar, ishdan ketish
 * sabablari, rag'bat/jarima va hujjat turlari) tuzilishi bo'yicha bir xil.
 * Har biriga alohida sahifa yozish — bir xil kodni oltita joyda saqlash
 * degani; farq faqat sarlavha, ikona va API yo'lida, ular esa `meta` da.
 */
export function CatalogPage({ meta }: { meta: CatalogKindMeta }) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(CATALOG_MANAGE_PERMISSION)

  const [query, setQuery] = useState('')
  const [onlyActive, setOnlyActive] = useState(true)
  const [editing, setEditing] = useState<CatalogItem | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)

  const list = useCatalogList(meta.kind, onlyActive)
  const toggle = useToggleCatalogItem(meta.kind)

  const toggleMutate = toggle.mutate
  /** Faqat bosilgan qatorning almashtirgichi «kutish» holatiga o'tadi. */
  const pendingId = toggle.isPending ? (toggle.variables?.id ?? null) : null

  const openCreate = useCallback(() => {
    setEditing(null)
    setDialogOpen(true)
  }, [])

  const openEdit = useCallback((item: CatalogItem) => {
    setEditing(item)
    setDialogOpen(true)
  }, [])

  const columns = useMemo<Array<DataColumn<CatalogItem>>>(() => {
    const definitions: Array<DataColumn<CatalogItem>> = [
      helper.accessor('name', {
        header: t('catalog.columnName'),
        cell: ({ row }) => (
          <div className="min-w-0">
            <p className="truncate-1 font-medium">{row.original.name}</p>
            {row.original.note !== null && row.original.note !== '' && (
              <p className="truncate-1 mt-0.5 text-caption1 text-[var(--fg-tertiary)]">
                {row.original.note}
              </p>
            )}
          </div>
        ),
      }),

      helper.accessor('code', {
        header: t('catalog.columnCode'),
        meta: { width: '180px', hideOnMobile: true },
        cell: ({ getValue }) => <CodeCell value={getValue()} />,
      }),

      helper.accessor('sortOrder', {
        header: t('catalog.columnOrder'),
        meta: { align: 'end', width: '120px', hideOnMobile: true },
        cell: ({ getValue }) => (
          <span className="tabular text-[var(--fg-secondary)]">{num(getValue())}</span>
        ),
      }),

      helper.display({
        id: 'status',
        header: t('catalog.columnStatus'),
        meta: { width: '190px' },
        cell: ({ row }) => (
          <CatalogStatus
            active={row.original.isActive}
            canManage={canManage}
            pending={pendingId === row.original.id}
            onToggle={(next) => toggleMutate({ id: row.original.id, active: next })}
          />
        ),
      }),
    ]

    // Ruxsat yo'q bo'lsa ustunning o'zi ham chiqmaydi — bo'sh ustun
    // «bu yerda nimadir bo'lishi kerak edi» degan taassurot qoldiradi
    if (canManage) {
      definitions.push(
        helper.display({
          id: 'actions',
          header: t('catalog.columnActions'),
          meta: { align: 'end', width: '72px' },
          cell: () => (
            // iOS ro'yxatidagi «ochiladi» belgisi: qator bosiladigan
            // ekanini alohida tugmasiz aytadi
            <ChevronRight
              className="ml-auto size-4 text-[var(--fg-tertiary)]"
              strokeWidth={2.5}
            />
          ),
        }),
      )
    }

    return definitions
  }, [t, num, canManage, pendingId, toggleMutate])

  const rows = useMemo(() => {
    const source = list.data ?? EMPTY
    const needle = query.trim().toLocaleLowerCase()
    if (needle === '') return source

    return source.filter(
      (item) =>
        item.name.toLocaleLowerCase().includes(needle) ||
        (item.code ?? '').toLocaleLowerCase().includes(needle),
    )
  }, [list.data, query])

  const searching = query.trim() !== ''

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={meta.icon} tone={meta.tone} size="lg" />
            {t(`catalog.kinds.${meta.i18nKey}.title`)}
          </span>
        }
        subtitle={t(`catalog.kinds.${meta.i18nKey}.subtitle`)}
        actions={
          canManage ? (
            <Button variant="create" onClick={openCreate}>
              <Plus />
              {t('common.add')}
            </Button>
          ) : undefined
        }
      />

      {list.isError && (
        <Alert tone="danger" title={t('catalog.loadFailed')}>
          {errorMessage(catalogErrorCode(list.error))}
        </Alert>
      )}

      <Card className="overflow-hidden">
        <CatalogToolbar
          query={query}
          onQueryChange={setQuery}
          onlyActive={onlyActive}
          onOnlyActiveChange={setOnlyActive}
        />

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(item) => item.id}
          loading={list.isPending}
          onRowClick={canManage ? openEdit : undefined}
          rowLabel={(item) => item.name}
          empty={
            searching ? (
              <EmptyState
                icon={SearchX}
                title={t('catalog.notFoundTitle')}
                description={t('catalog.notFoundText', { query: query.trim() })}
              />
            ) : (
              <EmptyState
                icon={meta.icon}
                title={t('catalog.emptyTitle')}
                description={t('catalog.emptyText')}
                action={
                  canManage ? (
                    <Button variant="create" onClick={openCreate}>
                      <Plus />
                      {t('common.add')}
                    </Button>
                  ) : undefined
                }
              />
            )
          }
        />

        {!list.isPending && rows.length > 0 && (
          <p className="border-t border-[var(--border)] px-5 py-3 text-caption1 text-[var(--fg-tertiary)]">
            {t('catalog.total', { n: num(rows.length) })}
          </p>
        )}
      </Card>

      <CatalogFormDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        kind={meta.kind}
        item={editing}
      />
    </div>
  )
}

/** Kod bo'sh bo'lsa chiziqcha — bo'sh katak «yuklanmadimi?» degan savol tug'diradi. */
function CodeCell({ value }: { value: string | null }) {
  if (value === null || value === '') {
    return <span className="text-[var(--fg-tertiary)]">—</span>
  }

  return (
    <span className="tabular rounded-xs bg-[var(--bg-sunken)] px-1.5 py-0.5 font-mono text-caption1 text-[var(--fg-secondary)]">
      {value}
    </span>
  )
}
