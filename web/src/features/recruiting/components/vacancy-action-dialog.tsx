import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea, TextField } from '@/shared/ui'
import type { Vacancy } from '../api/recruiting-api'
import { useChangeVacancyStatus, useDeleteVacancy } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

/**
 * Ro'yxatdan chaqiriladigan amallar — qaysi oyna ochilishini shu belgilaydi.
 *
 * Ilgari to'rtta edi: `pause` OLIB TASHLANDI, `resume` esa `reopen` ga
 * aylandi. Sabab ikkalasida ham bitta — to'xtatish va yopish AYNI
 * natijani berardi (batafsil backend'dagi `VacancyStatus` izohida), ya'ni
 * «davom ettiriladigan» narsa ham qolmadi.
 */
export type VacancyAction = 'close' | 'reopen' | 'delete'

/**
 * Vakansiya holatini o'zgartirish — UCHTA amal, BITTA oyna.
 *
 * NEGA uchta alohida komponent emas: ularning tuzilishi bir xil
 * (sarlavha + tushuntirish + ixtiyoriy sabab + tasdiqlash tugmasi) va
 * farq faqat MATNDA hamda yuboriladigan so'rovda. Uchga bo'linsa,
 * bir xil oyna qobig'i uch marta yozilardi.
 *
 * NEGA har biri baribir TASDIQLASH so'raydi:
 *   · «Yopish» — vakansiya yangi nomzod qabul qilmaydigan holatga
 *     o'tadi (server 409 bilan rad etadi), ya'ni tasodifan bosilsa
 *     ish to'xtaydi;
 *   · «Qayta ochish» — qaytariladigan, lekin u ham holat o'zgarishi va
 *     u ham ro'yxatdagi yorliqni almashtiradi;
 *   · «O'chirish» — YUMSHOQ o'chirish (yozuv bazada qoladi), lekin
 *     ro'yxatdan g'oyib bo'ladi.
 *
 * NEGA «yopish» va «o'chirish» ATAYLAB ajratilgan: ish tugagan
 * vakansiya YOPILADI (u tarixning bir qismi va nomzodlari unga
 * bog'langan), o'chirish esa faqat XATO kiritilgan — ya'ni NOMZODI
 * YO'Q — yozuv uchun. Buni server qat'iy tekshiradi
 * (`vakansiya.nomzodlari_bor`), ro'yxat esa bunday qatorda o'chirish
 * tugmasini umuman chizmaydi.
 */
export function VacancyActionDialog({
  action,
  item,
  onClose,
}: {
  /** `null` — oyna yopiq. */
  action: VacancyAction | null
  item: Vacancy | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const change = useChangeVacancyStatus()
  const remove = useDeleteVacancy()
  const pending = change.isPending || remove.isPending

  const [reason, setReason] = useState('')
  const [closedAt, setClosedAt] = useState(today)

  // Oldingi amalning matni va xato holati keyingisiga o'tib qolmasin.
  useEffect(() => {
    if (action === null) return
    setReason('')
    setClosedAt(today())
    change.reset()
    remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [action, item])

  if (action === null || item === null) return null

  const failure = change.error ?? remove.error

  const submit = () => {
    if (action === 'delete') {
      remove.mutate(item.id, { onSuccess: onClose })
      return
    }

    change.mutate(
      {
        id: item.id,
        action,
        reason: orNull(reason),
        // Yopish sanasi FAQAT «yopish» amalida yuboriladi: qayta
        // ochishda server bunday maydonni umuman kutmaydi — aksincha,
        // u `closedAt` ni TOZALAYDI.
        ...(action === 'close' ? { closedAt: orNull(closedAt) } : {}),
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!pending}
      title={t(`recruiting.vacancyAction.${action}Title`)}
      description={item.title ?? undefined}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            /*
              «Qayta ochish» — tiklovchi amal, ko'k tugma. Qolgan
              ikkitasi ro'yxatdan olib qo'yadi: qizil
              (`catalog-active-field.tsx` dagi bilan bir xil mulohaza —
              qaytariladigan, lekin sezilarli amal asosiy tugma o'rnida
              turadi).
            */
            variant={action === 'reopen' ? 'primary' : 'danger'}
            onClick={submit}
            disabled={pending}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {t(`recruiting.vacancyAction.${action}Confirm`)}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && failure !== undefined && (
          <Alert tone="danger">{errorMessage(recruitingErrorCode(failure))}</Alert>
        )}

        <p className="text-subhead text-[var(--fg-secondary)]">
          {t(`recruiting.vacancyAction.${action}Text`)}
        </p>

        {action === 'close' && (
          <TextField
            label={t('recruiting.closedAt')}
            type="date"
            value={closedAt}
            disabled={pending}
            onChange={(event) => setClosedAt(event.target.value)}
          />
        )}

        {/*
          Sabab «qayta ochish» va «o'chirish» da YO'Q va bu server
          shartnomasidan kelib chiqadi: `ReopenVacancyCommand` va
          `DeleteVacancyCommand` da bunday maydon umuman yo'q. Ko'rsatib
          qo'yilsa, HR yozgan matn jimgina yo'qolardi.

          Yopishda esa u ATAYLAB qoladi va endi avvalgidan muhimroq:
          holat «nega yopildi» degan savolga javob bermaydi (u ikkitaga
          qisqardi), javob faqat shu izohda qoladi.
        */}
        {action === 'close' && (
          <TextArea
            label={t('recruiting.reason')}
            placeholder={t('recruiting.reasonPlaceholder')}
            value={reason}
            disabled={pending}
            onChange={(event) => setReason(event.target.value)}
          />
        )}
      </div>
    </Dialog>
  )
}
