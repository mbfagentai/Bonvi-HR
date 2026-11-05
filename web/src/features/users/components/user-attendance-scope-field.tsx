import { useMemo } from 'react'

import type { DepartmentNode } from '@/features/catalog/api/catalog-api'
import { flattenTree } from '@/features/catalog/lib/tree'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Button, Checkbox, Skeleton } from '@/shared/ui'

/** Static classes: Tailwind cannot see a computed `pl-*`, and inline styles are not used here. */
const DEPTH_INDENT = ['', 'pl-5', 'pl-10', 'pl-15', 'pl-20'] as const

function indentOf(depth: number): string {
  return DEPTH_INDENT[Math.min(depth, DEPTH_INDENT.length - 1)] ?? ''
}

/**
 * «Qaysi bo'limlar» for a «Davomat mas'uli» account.
 *
 * Only what is ticked is stored: the server adds the descendants of each assigned department
 * itself, so ticking a parent does not tick its children here. Inactive departments are listed
 * too, otherwise one assigned earlier could not be unticked.
 */
export function UserAttendanceScopeField({
  departments,
  selected,
  onToggle,
  loading,
  failed,
  onRetry,
  disabled,
  error,
}: {
  departments: readonly DepartmentNode[]
  selected: readonly string[]
  onToggle: (departmentId: string, checked: boolean) => void
  loading: boolean
  /** The tree or the saved list could not be read. The dialog keeps Save disabled meanwhile. */
  failed: boolean
  onRetry: () => void
  disabled: boolean
  error: string | undefined
}) {
  const { t, num } = useLocale()
  const rows = useMemo(() => flattenTree(departments), [departments])
  const legend = t('users.attendanceScope')

  if (loading) {
    return (
      <div className="space-y-2" aria-busy>
        <Skeleton className="h-4 w-32" />
        <Skeleton className="h-32 w-full" />
      </div>
    )
  }

  if (failed) {
    return (
      <div className="space-y-2">
        <p className="text-footnote font-medium text-[var(--fg-secondary)]">{legend}</p>
        <Alert tone="danger" title={t('users.attendanceScopeLoadFailed')}>
          {t('users.attendanceScopeLoadFailedText')}
        </Alert>
        <Button type="button" size="sm" onClick={onRetry}>
          {t('common.retry')}
        </Button>
      </div>
    )
  }

  return (
    <fieldset className="space-y-2">
      <legend className="flex w-full items-baseline justify-between gap-3 text-footnote font-medium text-[var(--fg-secondary)]">
        {legend}
        {selected.length > 0 && (
          <span className="tabular font-normal text-[var(--fg-tertiary)]">
            {t('users.attendanceScopeSelected', { selected: num(selected.length) })}
          </span>
        )}
      </legend>

      {rows.length === 0 ? (
        <p className="rounded-md border border-[var(--border)] bg-[var(--bg-sunken)] px-3 py-4 text-footnote text-[var(--fg-secondary)]">
          {t('users.attendanceScopeEmpty')}
        </p>
      ) : (
        <ul
          className={cn(
            'max-h-56 space-y-2.5 overflow-y-auto overscroll-contain rounded-md border bg-[var(--bg-sunken)] p-3',
            error === undefined ? 'border-[var(--border)]' : 'border-ios-red',
          )}
        >
          {rows.map(({ node, depth }) => (
            <li key={node.id} className={indentOf(depth)}>
              <Checkbox
                checked={selected.includes(node.id)}
                disabled={disabled}
                onChange={(checked) => onToggle(node.id, checked)}
                label={
                  <span className="text-subhead">
                    {node.name}
                    {!node.isActive && (
                      <span className="text-[var(--fg-tertiary)]"> · {t('users.inactive')}</span>
                    )}
                  </span>
                }
              />
            </li>
          ))}
        </ul>
      )}

      {error !== undefined ? (
        <p role="alert" className="text-footnote text-ios-red">
          {error}
        </p>
      ) : (
        <p className="text-footnote text-[var(--fg-tertiary)]">{t('users.attendanceScopeHint')}</p>
      )}
    </fieldset>
  )
}
