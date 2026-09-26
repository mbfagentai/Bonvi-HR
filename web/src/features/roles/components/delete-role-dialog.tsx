import { LoaderCircle, Trash2 } from 'lucide-react'
import { useEffect } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog } from '@/shared/ui'

import type { RoleSummary } from '../api/roles-api'
import { useDeleteRole } from '../api/roles-queries'
import { roleDisplayName } from '../lib/role-presentation'

/**
 * O'chirishni tasdiqlash — `api-key-revoke-dialog.tsx` dagi bilan bir xil
 * naqsh.
 *
 * <b>Nega tasdiq kerak.</b> Rol QAYTARIB bo'lmaydigan tarzda o'chadi: uni
 * qayta yaratish mumkin, lekin ruxsatlarini bittalab qayta belgilash
 * kerak bo'ladi.
 *
 * ⚠️ Brauzerning `confirm()` si ISHLATILMAYDI: u tarjima qilinmaydi,
 * tungi rejimni bilmaydi va telefonda tizim oynasi bo'lib chiqadi.
 *
 * Rolda hisob bo'lsa server 409 (`rol.foydalanuvchisi_bor`) qaytaradi va
 * xato shu oynada tarjimasi bilan chiqadi. Muharrir bunday rolda tugmani
 * oldindan o'chirib qo'yadi — bu yerdagi xato faqat oraliqda kimdir rolni
 * berib ulgurgan holat uchun.
 */
export function DeleteRoleDialog({
  item,
  onClose,
}: {
  item: RoleSummary | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteRole()

  useEffect(() => {
    if (item !== null) remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const name = roleDisplayName(t, item)

  const submit = () => {
    remove.mutate(item.id, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('roleEditor.deleteTitle')}
      description={name}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={submit}
            disabled={remove.isPending}
          >
            {remove.isPending ? <LoaderCircle className="animate-spin" /> : <Trash2 />}
            {/* Amal tugmasi o'z obyektini nomlaydi — «Bekor qilish» bilan adashmasin. */}
            {remove.isPending ? t('roleEditor.deleting') : t('roleEditor.delete')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('roleEditor.deleteText', { name })}
        </p>

        {remove.isError && <Alert tone="danger">{errorMessage(problemCode(remove.error))}</Alert>}
      </div>
    </Dialog>
  )
}
