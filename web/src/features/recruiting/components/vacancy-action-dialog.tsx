import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea, TextField } from '@/shared/ui'
import type { Vacancy } from '../api/recruiting-api'
import { useChangeVacancyStatus, useDeleteVacancy } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

/** Ro'yxatdan chaqiriladigan amallar — qaysi oyna ochilishini shu belgilaydi. */
export type VacancyAction = 'pause' | 'resume' | 'close' | 'delete'

/**
 * Vakansiya holatini o'zgartirish — TO'RTTA amal, BITTA oyna.
 *
 * NEGA to'rtta alohida komponent emas: ularning tuzilishi bir xil
 * (sarlavha + tushuntirish + ixtiyoriy sabab + tasdiqlash tugmasi) va
 * farq faqat MATNDA hamda yuboriladigan so'rovda. To'rtga bo'linsa,
 * bir xil oyna qobig'i to'rt marta yozilardi.
 *
 * NEGA har biri baribir TASDIQLASH so'raydi:
 *   · «To'xtatish» va «Yopish» — vakansiyaga yangi nomzod kirita
 *     olmaydigan holatga o'tkazadi (server 409 bilan rad etadi), ya'ni
 *     tasodifan bosilsa ish to'xtaydi;
 *   · «Davom ettirish» — qaytariladigan, lekin sababi yozilmaydi va
 *     u ham holat o'zgarishi;
 *   · «O'chirish» — YUMSHOQ o'chirish (yozuv bazada qoladi, nomzodlari
 *     yo'qolmaydi), lekin ro'yxatdan g'oyib bo'ladi.
 *
 * NEGA «yopish» va «o'chirish» ATAYLAB ajratilgan: ish tugagan
 * vakansiya YOPILADI (u tarixning bir qismi va hisobotga tushadi),
 * o'chirish esa faqat XATO kiritilgan yozuv uchun. Backend hujjati ham
 * aynan shunday deydi.
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
        // Yopish sanasi FAQAT «yopish» amalida yuboriladi: qolgan
        // ikkitasida server bunday maydonni umuman kutmaydi.
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
      description={item.position ?? undefined}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            /*
              «Davom ettirish» — tiklovchi amal, ko'k tugma. Qolgan
              uchtasi ro'yxatdan olib qo'yadi: qizil
              (`catalog-active-field.tsx` dagi bilan bir xil mulohaza —
              qaytariladigan, lekin sezilarli amal asosiy tugma o'rnida
              turadi).
            */
            variant={action === 'resume' ? 'primary' : 'danger'}
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
          Sabab «davom ettirish» va «o'chirish» da YO'Q va bu server
          shartnomasidan kelib chiqadi: `ResumeVacancyCommand` va
          `DeleteVacancyCommand` da bunday maydon umuman yo'q. Ko'rsatib
          qo'yilsa, HR yozgan matn jimgina yo'qolardi.
        */}
        {(action === 'pause' || action === 'close') && (
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
