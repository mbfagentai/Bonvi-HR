import { ListTree, Plus, SearchX } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Button,
  Card,
  EmptyState,
  IconTile,
  PageHeader,
  Skeleton,
} from '@/shared/ui'
import { useDepartmentHeadNames, useDepartmentTree } from '../api/catalog-queries'
import type { DepartmentNode } from '../api/catalog-api'
import { catalogErrorCode } from '../lib/catalog-error'
import { CATALOG_MANAGE_PERMISSION, DEPARTMENTS_META } from '../lib/catalog-kinds'
import { countTree, filterTree, flattenTree } from '../lib/tree'
import { CatalogToolbar } from './catalog-toolbar'
import { DepartmentFormDialog } from './department-form-dialog'
import { DepartmentTree } from './department-tree'

const EMPTY: DepartmentNode[] = []

/**
 * Bo'limlar — daraxt ko'rinishida.
 *
 * NEGA jadval emas: bo'lim ierarxiyasi ma'noning bir qismi. «Ishlab
 * chiqarish → Vulkanizatsiya» bilan «Savdo → Vulkanizatsiya» bir xil nomli,
 * lekin butunlay boshqa bo'lim. Tekis ro'yxatda buni ajratib bo'lmaydi.
 *
 * Boshlang'ich holat — hammasi ochiq. 73 ta bo'lim uchun bu ko'p emas,
 * yopilgan daraxt esa «bo'sh» bo'lib ko'rinadi.
 */
export function DepartmentsPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(CATALOG_MANAGE_PERMISSION)

  const [query, setQuery] = useState('')
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(() => new Set())

  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<DepartmentNode | null>(null)
  const [parentForNew, setParentForNew] = useState<string | null>(null)

  /*
   * Butun daraxt, faol emas bo'limlar bilan birga. Faollik filtri ekrandan
   * olib tashlandi: yopilgan bo'lim daraxtdan g'oyib bo'lsa, uning ichidagi
   * bo'limlar ham ko'rinmay qolardi va tuzilma buzilgandek tuyulardi.
   */
  const tree = useDepartmentTree(false)

  // Boshliq ismi xodim kartasidan — kartani ko'rish ruxsati bo'lsagina (SPEC-jamoam §E)
  const headNames = useDepartmentHeadNames(hasPermission(EMPLOYEES_VIEW))

  const searching = query.trim() !== ''

  const filtered = useMemo(
    () => filterTree(tree.data ?? EMPTY, query),
    [tree.data, query],
  )
  const flat = useMemo(() => flattenTree(filtered), [filtered])

  // Qidiruv paytida daraxt to'liq ochiladi: topilgan bo'lim yopiq
  // shox ichida qolib ketmasligi kerak
  const visible = useMemo(
    () =>
      searching
        ? flat
        : flat.filter((row) => row.parents.every((id) => !collapsed.has(id))),
    [flat, collapsed, searching],
  )

  const branchIds = useMemo(
    () => flat.filter((row) => (row.node.children ?? []).length > 0).map((row) => row.node.id),
    [flat],
  )

  const toggleCollapsed = useCallback((id: string) => {
    setCollapsed((previous) => {
      const next = new Set(previous)
      if (!next.delete(id)) next.add(id)
      return next
    })
  }, [])

  const openCreate = useCallback((parentId: string | null) => {
    setEditing(null)
    setParentForNew(parentId)
    setDialogOpen(true)
  }, [])

  const openEdit = useCallback((node: DepartmentNode) => {
    setEditing(node)
    setParentForNew(null)
    setDialogOpen(true)
  }, [])

  const total = countTree(tree.data ?? EMPTY)
  const shown = countTree(filtered)

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={DEPARTMENTS_META.icon} tone={DEPARTMENTS_META.tone} size="lg" />
            {t('catalog.kinds.departments.title')}
          </span>
        }
        description={t('catalog.kinds.departments.subtitle')}
        actions={
          canManage ? (
            <Button variant="create" onClick={() => openCreate(null)}>
              <Plus />
              {t('catalog.departments.addRoot')}
            </Button>
          ) : undefined
        }
      />

      {tree.isError && (
        <Alert tone="danger" title={t('catalog.loadFailed')}>
          {errorMessage(catalogErrorCode(tree.error))}
        </Alert>
      )}

      <Card className="overflow-hidden">
        <CatalogToolbar
          query={query}
          onQueryChange={setQuery}
          trailing={
            branchIds.length > 0 && !searching ? (
              <Button
                variant="ghost"
                size="sm"
                onClick={() =>
                  setCollapsed((previous) =>
                    previous.size > 0 ? new Set() : new Set(branchIds),
                  )
                }
              >
                {collapsed.size > 0
                  ? t('catalog.departments.expandAll')
                  : t('catalog.departments.collapseAll')}
              </Button>
            ) : undefined
          }
        />

        {tree.isPending && (
          <div className="space-y-2 p-4">
            {[0, 1, 2, 3, 4, 5].map((index) => (
              <Skeleton
                key={index}
                className="h-9"
                // Chekinish daraxt shaklini yuklanish paytida ham ko'rsatadi
              />
            ))}
          </div>
        )}

        {!tree.isPending && visible.length === 0 && searching && (
          <EmptyState
            icon={SearchX}
            title={t('catalog.notFoundTitle')}
            description={t('catalog.notFoundText', { query: query.trim() })}
          />
        )}

        {!tree.isPending && visible.length === 0 && !searching && (
          <EmptyState
            icon={ListTree}
            title={t('catalog.departments.emptyTitle')}
            description={t('catalog.departments.emptyText')}
            action={
              canManage ? (
                <Button variant="create" onClick={() => openCreate(null)}>
                  <Plus />
                  {t('catalog.departments.addRoot')}
                </Button>
              ) : undefined
            }
          />
        )}

        {!tree.isPending && visible.length > 0 && (
          <DepartmentTree
            items={visible}
            collapsed={collapsed}
            onToggleCollapsed={toggleCollapsed}
            canManage={canManage}
            onSelect={openEdit}
            onAddChild={(parent) => openCreate(parent.id)}
            headNames={headNames}
          />
        )}

        {!tree.isPending && total > 0 && (
          <p className="border-t border-[var(--border)] px-5 py-3 text-caption1 text-[var(--fg-tertiary)]">
            {searching
              ? t('catalog.foundOf', { n: num(shown), total: num(total) })
              : t('catalog.total', { n: num(total) })}
          </p>
        )}
      </Card>

      <DepartmentFormDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        item={editing}
        defaultParentId={parentForNew}
      />
    </div>
  )
}
