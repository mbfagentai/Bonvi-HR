import { getRouteApi, Link } from '@tanstack/react-router'
import { ChevronRight, ClipboardList, Search, SearchX, ShieldCheck } from 'lucide-react'
import { useCallback, useMemo } from 'react'

import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useSearchDraft } from '@/shared/lib/list-search'
import { useClampPage, usePageSize } from '@/shared/lib/use-page-size'
import { Badge, Card, EmptyState, Input, Pagination, Select, Skeleton } from '@/shared/ui'

import type {
  EmployeeCompletenessItem,
  EmployeeCompletenessKind,
  EmployeeCompletenessSummary,
} from '../api/employee-completeness-api'
import {
  useEmployeeCompleteness,
  useEmployeeCompletenessSummary,
} from '../api/employee-completeness-queries'
import {
  completenessCardSearch,
  completenessPlace,
  isDocumentKind,
  primaryCompletenessKind,
} from '../lib/employee-completeness'
import { ProcessListSkeleton, ProcessLoadError } from './process-states'
import { ProcessTaskSection } from './process-task-card'

const route = getRouteApi('/_app/mening-ishlarim/')

type KindLabel = (code: string) => string

/** A document kind's name comes from the catalogue. One the summary no longer lists shows a dash, never its id. */
function useKindLabel(kinds: readonly EmployeeCompletenessKind[] | undefined): KindLabel {
  const { t } = useLocale()

  return useMemo(() => {
    const names = new Map(kinds?.map((kind) => [kind.code, kind.name]))
    return (code: string) =>
      isDocumentKind(code) ? (names.get(code) ?? '—') : t(`processes.docs.kinds.${code}`)
  }, [kinds, t])
}

function Summary({ summary }: { summary: EmployeeCompletenessSummary | undefined }) {
  const { t, num } = useLocale()

  if (summary === undefined) {
    return (
      <Card className="space-y-2.5 rounded-lg px-5 py-5" aria-busy="true">
        <Skeleton className="h-8 w-64 rounded-md" />
        <Skeleton className="h-4 w-40 rounded-md" />
      </Card>
    )
  }

  return (
    <Card className="rounded-lg px-5 py-5">
      <p className="text-title1 leading-tight font-bold">
        {t('processes.docs.heading', { n: num(summary.withGaps) })}
      </p>
      <p className="mt-1.5 text-subhead text-[var(--fg-secondary)]">
        {t('processes.docs.subheading', { n: num(summary.employees) })}
      </p>
    </Card>
  )
}

function KindChip({
  label,
  count,
  selected,
  onClick,
}: {
  label: string
  count: number
  selected: boolean
  onClick: () => void
}) {
  const { num } = useLocale()

  return (
    <button
      type="button"
      aria-pressed={selected}
      disabled={count === 0 && !selected}
      onClick={onClick}
      className={cn(
        'inline-flex min-h-8 max-w-full items-center gap-1.5 rounded-full border px-3 py-1 text-left text-footnote font-medium',
        'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
        selected
          ? 'border-transparent bg-[var(--accent-soft)] font-semibold text-[var(--accent-on-soft)]'
          : 'border-[var(--border)] bg-[var(--bg-elevated)] text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
        'disabled:cursor-default disabled:opacity-50 disabled:hover:bg-[var(--bg-elevated)] disabled:hover:text-[var(--fg-secondary)]',
      )}
    >
      <span className="min-w-0">{label}</span>
      <span className="tabular font-mono text-caption2">{num(count)}</span>
    </button>
  )
}

/**
 * One person, in the look of the task cards. The name is the card's one link, stretched over it, and goes to where the
 * main gap is entered. Each chip is a link of its own to where that gap is entered, above the stretched one.
 */
function EmployeeGapCard({
  item,
  filter,
  kindLabel,
}: {
  item: EmployeeCompletenessItem
  filter: string | undefined
  kindLabel: KindLabel
}) {
  const { t, num } = useLocale()

  const primary = primaryCompletenessKind(item.missing, filter)
  const subject = [item.position, item.department].filter(Boolean).join(', ')

  return (
    <Card
      className={cn(
        'relative rounded-lg border-l-4 border-l-ios-yellow',
        'transition-colors duration-[120ms] ease-[var(--ease-ios)] hover:bg-[var(--bg-hover)]',
      )}
    >
      <div className="flex flex-col gap-4 px-5 py-5 sm:flex-row sm:items-center">
        <div className="flex min-w-0 flex-1 items-start gap-4">
          <EmployeePhoto
            employeeId={item.employeeId}
            photoKey={item.hasPhoto ? item.employeeId : null}
            name={item.fullName}
            size="lg"
            className="shrink-0"
          />

          <div className="min-w-0 flex-1">
            <p className="tabular text-footnote text-[var(--fg-secondary)]">
              {t('processes.docs.progress', { done: num(item.done), total: num(item.total) })}
            </p>

            <h3 className="mt-1 text-title3 leading-snug font-semibold">
              <Link
                to="/xodimlar/$employeeId"
                params={{ employeeId: item.employeeId }}
                search={primary === null ? {} : completenessCardSearch(completenessPlace(primary))}
                className={cn(
                  'min-w-0 outline-none after:absolute after:inset-0 after:rounded-lg',
                  'focus-visible:after:ring-2 focus-visible:after:ring-[var(--accent)] focus-visible:after:ring-inset',
                )}
              >
                {item.fullName}
              </Link>
            </h3>

            {subject !== '' && (
              <p className="mt-1 text-subhead text-[var(--fg-secondary)]">{subject}</p>
            )}

            <ul className="mt-3 flex flex-wrap items-center gap-2">
              {item.missing.map((code) => {
                const place = completenessPlace(code)

                return (
                  <li key={code} className="relative z-10 max-w-full">
                    <Link
                      to="/xodimlar/$employeeId"
                      params={{ employeeId: item.employeeId }}
                      search={completenessCardSearch(place)}
                      title={t(`processes.docs.places.${place}`)}
                      className="block max-w-full rounded-full outline-none focus-visible:ring-2 focus-visible:ring-[var(--accent)]"
                    >
                      <Badge
                        tone="warning"
                        className="h-7 max-w-full px-3 text-footnote hover:opacity-80"
                      >
                        <span className="truncate">{kindLabel(code)}</span>
                      </Badge>
                    </Link>
                  </li>
                )
              })}
            </ul>
          </div>
        </div>

        <div className="flex shrink-0 items-center sm:flex-col sm:items-end">
          {/* Only the look of the action: the stretched name above is what takes the click and the focus. */}
          <span
            aria-hidden
            className="inline-flex h-9 items-center gap-1 rounded-full bg-[var(--accent-soft)] pr-2.5 pl-4 text-subhead font-semibold text-[var(--accent-on-soft)]"
          >
            {t('processes.docs.fill')}
            <ChevronRight strokeWidth={2.5} className="size-4" />
          </span>
        </div>
      </div>
    </Card>
  )
}

/**
 * «Hujjatlari chala»: current employees whose file lacks something, one card each, most gaps first. HR fills things in
 * where they belong and a person drops off this list by being complete; there is nothing to tick here.
 */
export function DocumentsTab() {
  const { t } = useLocale()
  const search = route.useSearch()
  const routeNavigate = route.useNavigate()
  const { departments } = useFilterOptions()
  const { pageSize, setPageSize, allowAll } = usePageSize('employee-completeness', false)

  type Search = ReturnType<typeof route.useSearch>

  const patchSearch = useCallback(
    (patch: (previous: Search) => Search, history: 'push' | 'replace' = 'replace') => {
      void routeNavigate({ search: patch, replace: history === 'replace' })
    },
    [routeNavigate],
  )

  const setFilter = useCallback(
    (patch: Partial<Search>) => {
      patchSearch((previous) => ({ ...previous, ...patch, page: undefined }))
    },
    [patchSearch],
  )

  const [draft, setDraft] = useSearchDraft(search.q ?? '', (value) => {
    setFilter({ q: value === '' ? undefined : value })
  })

  const page = search.page ?? 1
  const shared = { search: search.q, departmentId: search.department }
  const summary = useEmployeeCompletenessSummary(shared)
  const list = useEmployeeCompleteness({ ...shared, missing: search.missing, page, pageSize })

  const clampPage = useCallback(
    (last: number) => {
      patchSearch((previous) => ({ ...previous, page: last <= 1 ? undefined : last }))
    },
    [patchSearch],
  )
  useClampPage(page, pageSize, list.isPlaceholderData ? undefined : list.data?.total, clampPage)

  const kindLabel = useKindLabel(summary.data?.kinds)

  const filtered =
    search.q !== undefined || search.department !== undefined || search.missing !== undefined
  const items = list.data?.items ?? []
  const total = list.data?.total ?? 0

  return (
    <div className="space-y-5">
      <Summary summary={summary.data} />

      <Card className="space-y-3 rounded-lg px-4 py-3.5 sm:px-5">
        <div className="flex flex-wrap items-center gap-2.5 sm:gap-3">
          <div className="min-w-[200px] flex-1">
            <Input
              icon={Search}
              shape="pill"
              type="search"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              placeholder={t('processes.docs.searchPlaceholder')}
              aria-label={t('common.search')}
            />
          </div>

          <Select
            size="sm"
            aria-label={t('processes.docs.department')}
            className="w-full sm:w-[240px]"
            placeholder={t('processes.docs.allDepartments')}
            options={departments}
            value={search.department ?? ''}
            onChange={(event) => {
              const { value } = event.target
              setFilter({ department: value === '' ? undefined : value })
            }}
          />
        </div>

        {summary.data !== undefined && (
          <div role="group" aria-label={t('processes.docs.kindsLabel')} className="flex flex-wrap gap-2">
            <KindChip
              label={t('processes.docs.allKinds')}
              count={summary.data.withGaps}
              selected={search.missing === undefined}
              onClick={() => setFilter({ missing: undefined })}
            />
            {summary.data.kinds.map((kind) => (
              <KindChip
                key={kind.code}
                label={kindLabel(kind.code)}
                count={kind.count}
                selected={search.missing === kind.code}
                onClick={() =>
                  setFilter({ missing: search.missing === kind.code ? undefined : kind.code })
                }
              />
            ))}
          </div>
        )}
      </Card>

      {list.isError ? (
        <ProcessLoadError error={list.error} onRetry={() => void list.refetch()} />
      ) : list.isPending ? (
        <ProcessListSkeleton />
      ) : items.length === 0 ? (
        <Card>
          {filtered ? (
            <EmptyState
              icon={SearchX}
              title={t('processes.docs.notFoundTitle')}
              description={t('processes.docs.notFoundText')}
            />
          ) : (
            <EmptyState
              icon={ShieldCheck}
              title={t('processes.docs.emptyTitle')}
              description={t('processes.docs.emptyText')}
            />
          )}
        </Card>
      ) : (
        <ProcessTaskSection icon={ClipboardList} title={t('processes.docs.section')} count={total}>
          <div className="space-y-3" aria-busy={list.isPlaceholderData}>
            {items.map((item) => (
              <EmployeeGapCard
                key={item.employeeId}
                item={item}
                filter={search.missing}
                kindLabel={kindLabel}
              />
            ))}
          </div>

          <Card className="overflow-hidden rounded-lg">
            <Pagination
              page={page}
              pageSize={pageSize}
              total={total}
              onPageChange={(next) =>
                patchSearch(
                  (previous) => ({ ...previous, page: next <= 1 ? undefined : next }),
                  'push',
                )
              }
              onPageSizeChange={(next) => {
                setPageSize(next)
                patchSearch((previous) => ({ ...previous, page: undefined }))
              }}
              allowAll={allowAll}
              disabled={list.isFetching}
            />
          </Card>
        </ProcessTaskSection>
      )}
    </div>
  )
}
