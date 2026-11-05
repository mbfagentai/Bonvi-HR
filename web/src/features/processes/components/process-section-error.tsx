import { RefreshCw, TriangleAlert } from 'lucide-react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Button, EmptyState } from '@/shared/ui'

/** `ProcessLoadError` for a failure inside a card that is already drawn: the same words, without a card in a card. */
export function ProcessSectionError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  return (
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
  )
}
