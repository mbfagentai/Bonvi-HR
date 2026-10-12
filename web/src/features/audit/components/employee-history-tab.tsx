import { History } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useRoleLabels } from '@/features/roles/lib/role-presentation'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Card, EmptyState, Pagination } from '@/shared/ui'

import { useEmployeeHistory, type AuditEntry } from '../api/audit-queries'
import {
  AUDIT_PAGE_SIZE,
  EMPLOYEE_HISTORY_MODULES,
  canSeeAuditModule,
  type AuditModuleKey,
} from '../lib/audit-keys'
import { AuditEntryDialog } from './audit-entry-dialog'
import { AuditList } from './audit-list'

/** Modul darajasida — har render'da yangi massiv ro'yxat modelini buzadi. */
const NO_ENTRIES: AuditEntry[] = []

/**
 * Xodim kartasidagi «O'zgarishlar tarixi» (SPEC §9.6) — jurnal sahifasidagi
 * AYNAN o'sha ro'yxat, shu xodimga tegishli qatorlar bilan cheklangan.
 *
 * Egasining savoli shu yerda javob topishi kerak: «bu xodimning okladini kim
 * o'zgartirdi va oldin qancha edi» (SPEC §15, A2). Shu sababli davr filtri
 * YO'Q — oklad qarori yillar oldin bo'lgan bo'lishi mumkin (server ham davrsiz
 * o'qiydi). Filtr — bo'lim chiplari.
 *
 * Bo'lim va sahifa manzilga YOZILMAYDI: bu kartaning ichki holati, kartaning
 * o'z manzili (`?tab=history`) esa saqlanadi.
 *
 * Tab faqat `audit.view` bilan chiziladi — tekshiruv kartada (`$employeeId.tsx`).
 */
export function EmployeeHistoryTab({ employeeId }: { employeeId: string }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const labels = useRoleLabels()
  const { hasPermission } = useAuth()

  const [module, setModule] = useState<AuditModuleKey | null>(null)
  const [page, setPage] = useState(1)
  const [opened, setOpened] = useState<AuditEntry | null>(null)

  // Ko'ra olmaydigan bo'lim chipi doim bo'sh sahifa berardi — chizilmaydi
  const modules = useMemo(
    () => EMPLOYEE_HISTORY_MODULES.filter((key) => canSeeAuditModule(key, hasPermission)),
    [hasPermission],
  )

  const history = useEmployeeHistory(
    employeeId,
    { module: module ?? undefined, page, pageSize: AUDIT_PAGE_SIZE },
    true,
  )

  const entries = history.data?.items ?? NO_ENTRIES
  const total = history.data?.total ?? 0

  const choose = (next: AuditModuleKey | null) => {
    setModule(next)
    setPage(1)
  }

  return (
    <Card className="overflow-hidden">
      <div
        role="group"
        aria-label={t('audit.filterModule')}
        className="flex flex-wrap gap-1 border-b border-[var(--border)] px-4 py-3 sm:px-5"
      >
        <ModuleChip label={t('common.all')} selected={module === null} onClick={() => choose(null)} />
        {modules.map((key) => (
          <ModuleChip
            key={key}
            label={labels.module(key)}
            selected={module === key}
            onClick={() => choose(key)}
          />
        ))}
      </div>

      {history.isError ? (
        <div className="px-4 py-4 sm:px-5">
          <Alert tone="danger" title={t('audit.loadFailed')}>
            {errorMessage(problemCode(history.error))}
          </Alert>
        </div>
      ) : (
        <AuditList
          entries={entries}
          loading={history.isPending}
          variant="employee"
          onOpen={setOpened}
          empty={
            module === null ? (
              <EmptyState
                icon={History}
                title={t('audit.historyEmptyTitle')}
                description={t('audit.historyEmptyText')}
              />
            ) : (
              <EmptyState
                icon={History}
                title={t('audit.historyModuleEmpty', { module: labels.module(module) })}
                description={t('audit.notFoundText')}
              />
            )
          }
        />
      )}

      {!history.isError && total > 0 && (
        <Pagination
          page={page}
          pageSize={AUDIT_PAGE_SIZE}
          total={total}
          onPageChange={setPage}
          disabled={history.isFetching}
          className="border-t border-[var(--border)]"
        />
      )}

      <AuditEntryDialog entry={opened} onClose={() => setOpened(null)} onOpen={setOpened} />
    </Card>
  )
}

/**
 * Bo'lim chipi — davomat taxtasidagi holat chiplari bilan bir xil ko'rinish
 * (`attendance-dashboard-page.tsx`, `StatusChip`): tanlangani tint bilan,
 * qolganlari sokin kulrang fonda.
 */
function ModuleChip({
  label,
  selected,
  onClick,
}: {
  label: string
  selected: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onClick}
      className={cn(
        'press flex h-7 items-center rounded-full px-2.5 text-caption1 font-medium whitespace-nowrap',
        'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
        selected
          ? 'bg-[var(--accent-soft)] text-[var(--accent-on-soft)]'
          : 'bg-[var(--bg-sunken)] text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
      )}
    >
      {label}
    </button>
  )
}
