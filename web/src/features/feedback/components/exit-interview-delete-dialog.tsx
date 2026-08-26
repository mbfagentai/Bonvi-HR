import { LoaderCircle } from 'lucide-react'
import { useEffect } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog } from '@/shared/ui'

import { useDeleteExitInterview } from '../api/exit-interviews-queries'
import type { ExitInterview } from '../api/exit-interviews-api'
import { feedbackErrorCode } from '../lib/feedback-error'

/** O'chirishni tasdiqlash — `incident-delete-dialog.tsx` dagi bilan bir xil naqsh. */
export function ExitInterviewDeleteDialog({
  item,
  onClose,
}: {
  item: ExitInterview | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteExitInterview()

  useEffect(() => {
    if (item !== null) remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const submit = () => {
    remove.mutate(item.id, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('feedback.deleteTitle')}
      description={item.employeeName ?? undefined}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>
          <Button type="button" variant="danger" onClick={submit} disabled={remove.isPending}>
            {remove.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.delete')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">{t('feedback.deleteConfirm')}</p>

        {remove.isError && <Alert tone="danger">{errorMessage(feedbackErrorCode(remove.error))}</Alert>}
      </div>
    </Dialog>
  )
}
