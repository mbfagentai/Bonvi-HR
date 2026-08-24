import { ChevronRight, Plus, SearchX } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import {
  useCatalogList,
  useDepartmentTree,
  useToggleCatalogItem,
} from '../api/catalog-queries'
import type { DepartmentNode, PositionItem } from '../api/catalog-api'
import { catalogErrorCode } from '../lib/catalog-error'
import { CATALOG_MANAGE_PERMISSION, POSITIONS_META } from '../lib/catalog-kinds'
import { flattenTree } from '../lib/tree'
import { CatalogStatus } from './catalog-status'
import { CatalogToolbar } from './catalog-toolbar'
import { PositionFormDialog } from './position-form-dialog'

const helper = dataColumnHelper<PositionItem>()

const EMPTY: PositionItem[] = []
const EMPTY_TREE: DepartmentNode[] = []

/**
 * Lavozimlar ro'yxati.
 *
 * Oddiy ma'lumotnomadan farqi: lavozimda bo'lim, daraja va vazifalar bor.
 * Vazifalar soni ustuni ataylab ro'yxatda — HR uchun «vazifasi yozilmagan
 * lavozim» ishga qabulda muammo bo'ladi, uni bir qarashda ko'rish kerak.
 */
export function PositionsPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(CATALOG_MANAGE_PERMISSION)

  const [query, setQuery] = useState('')
  const [onlyActive, setOnlyActive] = useState(true)
  const [editing, setEditing] = useState<PositionItem | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)

  const list = useCatalogList<PositionItem>('positions', onlyActive)
  const toggle = useToggleCatalogItem('positions')

  /**
   * Bo'lim nomi lavozim yozuvida yo'q — faqat `departmentId` keladi.
   * Daraxt baribir tahrirlash oynasi uchun yuklanadi, shu sababli nom
   * shu yerda yechiladi: har bir qator uchun alohida so'rov (N+1) bo'lmaydi.
   */
  const departments = useDepartmentTree(false)
  const departmentNames = useMemo(() => {
    const map = new Map<string, string>()
    for (const row of flattenTree(departments.data ?? EMPTY_TREE)) {
      map.set(row.node.id, row.node.path ?? row.node.name)
    }
    return map
  }, [departments.data])

  const toggleMutate = toggle.mutate
  const pendingId = toggle.isPending ? (toggle.variables?.id ?? null) : null

  const openCreate = useCallback(() => {
    setEditing(null)
    setDialogOpen(true)
  }, [])

  const openEdit = useCallback((item: PositionItem) => {
    setEditing(item)
    setDialogOpen(true)
  }, [])

  const columns = useMemo<Array<DataColumn<PositionItem>>>(() => {
    const definitions: Array<DataColumn<PositionItem>> = [
      // Kengligi belgilanmagan yagona ustun — qolganlaridan ortgan joyni
      // shu oladi. Lavozim nomlari uzun bo'ladi («Katta smena ustasi»),
      // shu sababli eng keng joy aynan shu ustunga tegishi kerak.
      helper.accessor('name', {
        header: t('catalog.columnName'),
        cell: ({ getValue }) => <p className="truncate-1 font-medium">{getValue()}</p>,
      }),

      helper.accessor((row) => departmentNames.get(row.departmentId ?? '') ?? '', {
        id: 'department',
        header: t('catalog.positions.department'),
        meta: { width: '260px', hideOnMobile: true },
        cell: ({ getValue }) => {
          const value = getValue()
          return value === '' ? (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ) : (
            <span className="truncate-1 text-[var(--fg-secondary)]">{value}</span>
          )
        },
      }),

      helper.accessor('grade', {
        header: t('catalog.positions.grade'),
        meta: { align: 'end', width: '110px', hideOnMobile: true },
        cell: ({ getValue }) => (
          <span className="tabular text-[var(--fg-secondary)]">{num(getValue())}</span>
        ),
      }),

      helper.accessor('dutyCount', {
        header: t('catalog.positions.dutiesCount'),
        meta: { align: 'end', width: '140px' },
        cell: ({ getValue }) => {
          const count = getValue()
          // Server bu maydonni hali yubormaydi — yo'qligini «0» deb
          // ko'rsatish yolg'on bo'lardi, shu sababli chiziqcha
          if (count === null || count === undefined) {
            return <span className="text-[var(--fg-tertiary)]">—</span>
          }
          // Vazifasiz lavozim — HR uchun ogohlantirish, oddiy nol emas
          return (
            <Badge tone={count > 0 ? 'info' : 'warning'} size="sm">
              {num(count)}
            </Badge>
          )
        },
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

    if (canManage) {
      definitions.push(
        helper.display({
          id: 'actions',
          header: t('catalog.columnActions'),
          meta: { align: 'end', width: '72px' },
          cell: () => (
            <ChevronRight
              className="ml-auto size-4 text-[var(--fg-tertiary)]"
              strokeWidth={2.5}
            />
          ),
        }),
      )
    }

    return definitions
  }, [t, num, canManage, pendingId, toggleMutate, departmentNames])

  const rows = useMemo(() => {
    const source = list.data ?? EMPTY
    const needle = query.trim().toLocaleLowerCase()
    if (needle === '') return source

    // Nom va bo'lim nomi — ekranda ko'rinib turgan ikkala matn. Bir xil
    // nomli lavozim turli bo'limlarda uchraydi, shu sababli bo'lim bo'yicha
    // qidirish HR uchun kerakli qisqartma («ta'mirlash» → o'sha bo'lim ichi).
    return source.filter(
      (item) =>
        item.name.toLocaleLowerCase().includes(needle) ||
        (departmentNames.get(item.departmentId ?? '') ?? '')
          .toLocaleLowerCase()
          .includes(needle),
    )
  }, [list.data, query, departmentNames])

  const searching = query.trim() !== ''

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={POSITIONS_META.icon} tone={POSITIONS_META.tone} size="lg" />
            {t('catalog.kinds.positions.title')}
          </span>
        }
        subtitle={t('catalog.kinds.positions.subtitle')}
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
                icon={POSITIONS_META.icon}
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

      <PositionFormDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        item={editing}
        canManage={canManage}
      />
    </div>
  )
}
