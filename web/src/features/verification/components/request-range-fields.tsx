import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import type { DateRange } from '@/shared/lib/list-search'
import { Alert, DateField } from '@/shared/ui'

import { isRangeInverted } from '../lib/attendance-search'

/**
 * So'rovlar navbatining «sanadan–sanagacha» maydonlari — IKKALA navbatda
 * (yo'qlik va jadval o'zgarishi) bir xil.
 *
 * Fragment qaytaradi: maydonlar chaqiruvchining o'z panjarasiga
 * (`grid`) tushadi — yo'qlik navbatida tur va shakl tanlovi yonida,
 * jadval navbatida esa yolg'iz.
 *
 * Yorliqlar UMUMIY («Sanadan» / «Sanagacha», hisobotlar sahifasidagi
 * kabi), ma'nosi esa ostidagi izohda (<see cref="RequestRangeNote"/>):
 * oraliq so'rov DAVRIGA qisman tegsa ham so'rov chiqadi — buni bitta
 * yorliqqa sig'dirib bo'lmaydi.
 */
export function RequestRangeFields({
  range,
  onChange,
}: {
  range: DateRange
  onChange: (next: DateRange) => void
}) {
  const { t } = useLocale()

  return (
    <>
      {/* Chegaralar bir-birini taqvimda cheklaydi; qo'lda terilgan teskari
          oraliqni izoh ushlaydi va so'rov yuborilmaydi */}
      <DateField
        label={t('common.dateFrom')}
        value={range.from}
        max={range.to === '' ? undefined : range.to}
        onChange={(value) => onChange({ ...range, from: value })}
      />
      <DateField
        label={t('common.dateTo')}
        value={range.to}
        min={range.from === '' ? undefined : range.from}
        onChange={(value) => onChange({ ...range, to: value })}
      />
    </>
  )
}

/**
 * Maydonlar ostidagi izoh: oraliqning MA'NOSI va — teskari bo'lsa — xato.
 *
 * Teskari oraliq serverning o'z kodi bilan aytiladi (`davomat.davr_notogri`,
 * hisobotlar sahifasidagi kabi), ya'ni gap ikki joyda bir xil so'z bilan.
 */
export function RequestRangeNote({ range }: { range: DateRange }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  if (isRangeInverted(range.from, range.to)) {
    return (
      <Alert tone="warning" className="mt-3.5">
        {errorMessage('davomat.davr_notogri')}
      </Alert>
    )
  }

  return (
    <p className="mt-2.5 text-footnote text-[var(--fg-tertiary)]">
      {t('attendance.requestsRangeHint')}
    </p>
  )
}
