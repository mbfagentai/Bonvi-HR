import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import type { DateRange } from '@/shared/lib/list-search'
import { Alert, DateRangeField } from '@/shared/ui'

import { isRangeInverted } from '../lib/attendance-search'

/**
 * So'rovlar navbatining «sanadan–sanagacha» maydonlari — IKKALA navbatda
 * (yo'qlik va jadval o'zgarishi) bir xil.
 *
 * Bitta katak: yorliq «Davr», ostida ikki sana yonma-yon (`DateRangeField`,
 * egasi 2026-10-22 — sana filtri hamma ro'yxatda bir xil). Chaqiruvchining
 * o'z panjarasiga (`grid`) tushadi — yo'qlik navbatida tur va shakl tanlovi
 * yonida, jadval navbatida esa yolg'iz.
 *
 * Ma'nosi ostidagi izohda (<see cref="RequestRangeNote"/>): oraliq so'rov
 * DAVRIGA qisman tegsa ham so'rov chiqadi — buni bitta yorliqqa sig'dirib
 * bo'lmaydi.
 */
export function RequestRangeFields({
  range,
  onChange,
}: {
  range: DateRange
  onChange: (next: DateRange) => void
}) {
  const { t } = useLocale()

  // Qo'lda terilgan teskari oraliqni izoh ushlaydi va so'rov yuborilmaydi
  return (
    <DateRangeField
      label={t('attendance.requestPeriod')}
      fromLabel={t('common.dateFrom')}
      toLabel={t('common.dateTo')}
      from={range.from}
      to={range.to}
      onFromChange={(value) => onChange({ ...range, from: value })}
      onToChange={(value) => onChange({ ...range, to: value })}
    />
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
