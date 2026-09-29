import { LoaderCircle } from 'lucide-react'
import { useEffect } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog } from '@/shared/ui'
import type { Vacancy } from '../api/recruiting-api'
import { useDeleteVacancy } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'

/**
 * Vakansiyani o'chirish — TASDIQLASH oynasi.
 *
 * <b>Nega faqat o'chirish.</b> Ilgari bu oyna yopish va qayta ochishni
 * ham ko'rsatardi (sana va sabab bilan). Egasining qarori — vakansiya
 * eng sodda shaklda: holat ro'yxatdagi tugma bilan BIR BOSISHDA
 * almashadi (`vacancies-page.tsx`), chunki ikkalasi ham qaytariladigan
 * amal va nomzodlarga tegmaydi. O'chirish esa qatorni ro'yxatdan
 * butunlay olib tashlaydi — shu sababli yagona tasdiqlanadigan amal
 * bo'lib qoldi.
 *
 * NEGA «yopish» va «o'chirish» ATAYLAB ajratilgan: ish tugagan
 * vakansiya YOPILADI (u tarixning bir qismi va nomzodlari unga
 * bog'langan), o'chirish esa faqat XATO kiritilgan — ya'ni NOMZODI
 * YO'Q — yozuv uchun. Buni server qat'iy tekshiradi
 * (`vakansiya.nomzodlari_bor`), ro'yxat esa bunday qatorda o'chirish
 * tugmasini umuman chizmaydi.
 */
export function VacancyDeleteDialog({
  item,
  onClose,
}: {
  /** `null` — oyna yopiq. */
  item: Vacancy | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteVacancy()

  // Oldingi urinishning xatosi keyingi vakansiyaga o'tib qolmasin.
  useEffect(() => {
    if (item === null) return
    remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('recruiting.vacancyAction.deleteTitle')}
      description={item.title}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={() => remove.mutate(item.id, { onSuccess: onClose })}
            disabled={remove.isPending}
          >
            {remove.isPending && <LoaderCircle className="animate-spin" />}
            {t('recruiting.vacancyAction.deleteConfirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {remove.isError && (
          <Alert tone="danger">{errorMessage(recruitingErrorCode(remove.error))}</Alert>
        )}

        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('recruiting.vacancyAction.deleteText')}
        </p>
      </div>
    </Dialog>
  )
}
