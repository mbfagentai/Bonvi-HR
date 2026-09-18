import { Ban, LoaderCircle } from 'lucide-react'
import { useEffect } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog } from '@/shared/ui'

import type { ApiKeySummary } from '../api/api-keys-api'
import { useRevokeApiKey } from '../api/api-keys-queries'

/**
 * Bekor qilishni tasdiqlash — `deactivate-user-dialog.tsx` dagi bilan bir
 * xil naqsh.
 *
 * <b>Nega tasdiq umuman kerak.</b> Kalit DARHOL ishlamay qoladi va uni
 * qaytarib bo'lmaydi: yagona chora — yangi kalit yaratib, uni tashqi
 * loyihaga qayta qo'yish. Ya'ni tasodifan bosilgan tugma boshqa
 * loyihaning ishini to'xtatardi.
 *
 * ⚠️ Brauzerning `confirm()` si ISHLATILMAYDI: u tarjima qilinmaydi,
 * tungi rejimni bilmaydi va telefonda tizim oynasi bo'lib chiqadi —
 * loyihaning o'z `Dialog` i esa hamma joyda bir xil ko'rinadi.
 */
export function ApiKeyRevokeDialog({
  item,
  onClose,
}: {
  item: ApiKeySummary | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const revoke = useRevokeApiKey()

  useEffect(() => {
    if (item !== null) revoke.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const submit = () => {
    revoke.mutate(item.id, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!revoke.isPending}
      title={t('settings.apiKeys.revokeTitle')}
      description={item.name}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={revoke.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={submit}
            disabled={revoke.isPending}
          >
            {revoke.isPending ? <LoaderCircle className="animate-spin" /> : <Ban />}
            {/*
              ⚠️ Tugma «Bekor qilish» EMAS, «Kalitni bekor qilish».

              O'zbek tilida «bekor qilish» ikki ma'noda ishlatiladi:
              oynani YOPISH (`common.cancel`) va kalitni O'CHIRIB
              QO'YISH. Ikkala tugma bir xil yozilganda ular yonma-yon
              turardi va qaysi biri qaytarib bo'lmaydigani ko'rinmasdi
              (brauzerda tekshirilgan). `ResetPasswordDialog` dagi bilan
              bir xil qoida: amal tugmasi o'z obyektini nomlaydi.
            */}
            {revoke.isPending
              ? t('settings.apiKeys.revoking')
              : t('settings.apiKeys.revokeConfirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('settings.apiKeys.revokeText', { name: item.name })}
        </p>

        {revoke.isError && <Alert tone="danger">{errorMessage(problemCode(revoke.error))}</Alert>}
      </div>
    </Dialog>
  )
}
