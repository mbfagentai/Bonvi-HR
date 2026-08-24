import { ChevronRight, Plus } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui'
import type { DepartmentNode } from '../api/catalog-api'
import type { FlatDepartment } from '../lib/tree'
import { CatalogStatus } from './catalog-status'

/** Har bir daraja shuncha piksel ichkariga suriladi. */
const INDENT = 26

/**
 * Bo'limlar daraxti.
 *
 * NEGA rekursiv emas, tekis ro'yxat: yopilgan shoxni chizmaslik, klaviatura
 * bilan yurish va qidiruv natijasini ko'rsatish — uchalasi ham tekis
 * ro'yxatda bitta `filter` bilan hal bo'ladi. Rekursiyada har biri uchun
 * daraxtni qaytadan aylanib chiqishga to'g'ri kelardi.
 *
 * Chuqurlik chekinish bilan beriladi, chegara chizig'i bilan emas: iOS'da
 * ierarxiya bo'sh joy orqali o'qiladi.
 */
export function DepartmentTree({
  items,
  collapsed,
  onToggleCollapsed,
  canManage,
  onSelect,
  onAddChild,
  onToggleActive,
  pendingId,
}: {
  items: readonly FlatDepartment[]
  collapsed: ReadonlySet<string>
  onToggleCollapsed: (id: string) => void
  canManage: boolean
  onSelect: (node: DepartmentNode) => void
  onAddChild: (parent: DepartmentNode) => void
  onToggleActive: (node: DepartmentNode, next: boolean) => void
  pendingId: string | null
}) {
  const { t } = useLocale()

  return (
    <ul role="tree" aria-label={t('catalog.kinds.departments.title')} className="p-2">
      {items.map(({ node, depth }) => {
        const children = node.children ?? []
        const hasChildren = children.length > 0
        const open = hasChildren && !collapsed.has(node.id)

        return (
          <li
            key={node.id}
            role="treeitem"
            aria-level={depth + 1}
            aria-expanded={hasChildren ? open : undefined}
            className="list-none"
          >
            <div
              className={cn(
                'group flex items-center gap-2 rounded-md py-1.5 pr-2 transition-colors',
                'hover:bg-[var(--bg-hover)]',
              )}
              style={{ paddingLeft: 8 + depth * INDENT }}
            >
              {hasChildren ? (
                <button
                  type="button"
                  onClick={() => onToggleCollapsed(node.id)}
                  aria-label={open ? t('catalog.departments.collapse') : t('catalog.departments.expand')}
                  className="press grid size-7 shrink-0 place-items-center rounded-sm text-[var(--fg-tertiary)] hover:bg-[var(--bg-active)] hover:text-[var(--fg)]"
                >
                  <ChevronRight
                    className={cn(
                      'size-4 transition-transform duration-200 ease-[var(--ease-ios)]',
                      open && 'rotate-90',
                    )}
                    strokeWidth={2.5}
                  />
                </button>
              ) : (
                // Bo'sh joy — nomlar bir vertikal chiziqda tursin
                <span aria-hidden className="size-7 shrink-0" />
              )}

              <button
                type="button"
                onClick={() => onSelect(node)}
                disabled={!canManage}
                className={cn(
                  'flex min-w-0 flex-1 items-center gap-2.5 rounded-sm py-1 text-left',
                  canManage ? 'cursor-pointer' : 'cursor-default',
                )}
              >
                <span
                  className={cn(
                    'truncate-1 text-subhead',
                    // Ildiz bo'limlar biroz to'yingroq — daraxt boshi ko'rinib tursin
                    depth === 0 ? 'font-semibold' : 'font-medium',
                  )}
                >
                  {node.name}
                </span>

                {node.code !== null && node.code !== '' && (
                  <span className="tabular hidden shrink-0 rounded-xs bg-[var(--bg-sunken)] px-1.5 py-0.5 font-mono text-caption2 text-[var(--fg-secondary)] sm:inline">
                    {node.code}
                  </span>
                )}

                {hasChildren && (
                  <span className="hidden shrink-0 text-caption2 text-[var(--fg-tertiary)] md:inline">
                    {t('catalog.departments.childCount', { n: children.length })}
                  </span>
                )}
              </button>

              <div className="flex shrink-0 items-center gap-1.5">
                <CatalogStatus
                  active={node.isActive}
                  canManage={canManage}
                  pending={pendingId === node.id}
                  onToggle={(next) => onToggleActive(node, next)}
                />

                {canManage && (
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => onAddChild(node)}
                    title={t('catalog.departments.addChild')}
                    aria-label={t('catalog.departments.addChild')}
                    // Doim ko'rinadi: sensorli ekranda «sichqoncha ustiga
                    // kelganda paydo bo'ladigan» tugmaga umuman yetib
                    // bo'lmaydi. Ghost variantining o'zi yetarlicha xolis.
                  >
                    <Plus />
                  </Button>
                )}
              </div>
            </div>
          </li>
        )
      })}
    </ul>
  )
}
