import { Button, Dialog } from '@/shared/ui'
import { useLocale } from '@/shared/i18n/use-locale'
import type { Vacancy } from '../api/recruiting-api'

/**
 * «Umumiy anketa» ni yopish — TASDIQLASH oynasi.
 *
 * <b>Nega faqat shu vakansiyada oyna bor.</b> Oddiy vakansiya bir bosishda
 * yopiladi (`vacancies-page.tsx`): oqibati ko'rinib turadi — e'lon botdagi
 * ro'yxatdan tushadi. «Umumiy anketa» ning oqibati esa ko'rinmaydi: botdagi
 * «📋 Anketa to'ldirish» ham yopiladi. 2026-09 da u shunday yopilib qolgan va
 * 18 kun davomida nomzodlar ariza bera olmagan — buni hech kim sezmagan
 * (backend `GeneralPoolVacancy` izohi). Egasining qarori (2026-10-05): uni
 * yopish mumkin, lekin oqibati yopishdan OLDIN aytiladi.
 *
 * Qayta ochish oynasiz — u hech narsani to'xtatmaydi.
 */
export function GeneralPoolCloseDialog({
  item,
  pending,
  onConfirm,
  onClose,
}: {
  /** `null` — oyna yopiq. */
  item: Vacancy | null
  pending: boolean
  onConfirm: (item: Vacancy) => void
  onClose: () => void
}) {
  const { t } = useLocale()

  if (item === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!pending}
      title={t('recruiting.generalPoolClose.title')}
      description={item.title}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button type="button" variant="danger" onClick={() => onConfirm(item)} disabled={pending}>
            {t('recruiting.generalPoolClose.confirm')}
          </Button>
        </>
      }
    >
      <p className="text-subhead text-[var(--fg-secondary)]">{t('recruiting.generalPoolClose.text')}</p>
    </Dialog>
  )
}
