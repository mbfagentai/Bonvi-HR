import { RefreshCw, ShieldCheck, TriangleAlert, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Button, Card, EmptyState, Skeleton } from '@/shared/ui'

/** The deliberate states every screen of the module shares (loading, failed, forbidden, not built yet). */

/** Shown instead of a screen's content when the viewer lacks its permission. */
export function ProcessNoAccess() {
  const { t } = useLocale()

  return (
    <Card>
      <EmptyState
        icon={ShieldCheck}
        title={t('processes.noAccessTitle')}
        description={t('processes.noAccessText')}
      />
    </Card>
  )
}

/** A failed read: the server's translated reason and a way to try again. */
export function ProcessLoadError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  return (
    <Card>
      <EmptyState
        icon={TriangleAlert}
        title={t('processes.loadFailed')}
        description={errorMessage(problemCode(error))}
        action={
          <Button variant="secondary" onClick={onRetry}>
            <RefreshCw />
            {t('common.retry')}
          </Button>
        }
      />
    </Card>
  )
}

/** Rows of a list still on their way. */
export function ProcessListSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-2.5" aria-busy="true">
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} className="h-20 w-full rounded-lg" />
      ))}
    </div>
  )
}

/** A screen whose slice is not built yet. */
export function ProcessPlaceholder({
  icon,
  title,
  description,
}: {
  icon: LucideIcon
  title: ReactNode
  description: ReactNode
}) {
  return (
    <Card>
      <EmptyState icon={icon} title={title} description={description} />
    </Card>
  )
}
