import { LoaderCircle } from 'lucide-react'
import { useEffect } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog } from '@/shared/ui'

import type { UserSummary } from '../api/users-api'
import { useDeactivateUser } from '../api/users-queries'
import { userErrorCode } from '../lib/user-error'

/**
 * Faolsizlantirishni tasdiqlash — xavfli amal, bexosdan bosilmasin.
 *
 * `catalog-active-field.tsx` dagi tasdiqlash oynasi bilan bir xil naqsh:
 * hisob O'CHIRILMAYDI, faqat kirish yopiladi va istalgan vaqtda qaytadan
 * faollashtirish mumkin — shu sababli matn ogohlantirish emas, aniqlik.
 */
export function DeactivateUserDialog({
  item,
  onClose,
}: {
  item: UserSummary | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const deactivate = useDeactivateUser()

  useEffect(() => {
    if (item !== null) deactivate.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const submit = () => {
    deactivate.mutate(item.id, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!deactivate.isPending}
      title={t('users.confirmDeactivateTitle')}
      description={item.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={deactivate.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={submit}
            disabled={deactivate.isPending}
          >
            {deactivate.isPending && <LoaderCircle className="animate-spin" />}
            {deactivate.isPending ? t('users.deactivating') : t('users.deactivate')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('users.confirmDeactivateText', { name: item.fullName })}
        </p>

        {deactivate.isError && (
          <Alert tone="danger">{errorMessage(userErrorCode(deactivate.error))}</Alert>
        )}
      </div>
    </Dialog>
  )
}
