import { TriangleAlert } from 'lucide-react'
import type { ReactNode } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog } from '@/shared/ui'

import type { HrWarning } from '../lib/hr-warnings'

/**
 * The write went through and the server has something to say about it. Shown instead of closing, because these warnings
 * arrive only in this reply (the same result view as `candidate-hire-dialog.tsx`).
 */
export function HrOutcomeDialog({
  title,
  description,
  done,
  warnings,
  onClose,
}: {
  title: ReactNode
  description?: ReactNode
  done: ReactNode
  warnings: readonly HrWarning[]
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  return (
    <Dialog
      open
      onClose={onClose}
      title={title}
      description={description}
      footer={
        <Button type="button" variant="primary" onClick={onClose}>
          {t('common.close')}
        </Button>
      }
    >
      <div className="space-y-4">
        <Alert tone="success">{done}</Alert>

        <Alert tone="warning" title={t('employees.outcomeWarnings')}>
          <ul className="mt-1 space-y-1.5">
            {warnings.map((warning) => (
              <li key={warning.code} className="flex gap-2 text-footnote">
                <TriangleAlert className="mt-0.5 size-3.5 shrink-0" strokeWidth={2} />
                <span>{errorMessage(warning.code, warning.message)}</span>
              </li>
            ))}
          </ul>
        </Alert>
      </div>
    </Dialog>
  )
}
