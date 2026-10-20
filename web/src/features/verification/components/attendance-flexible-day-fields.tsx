import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Checkbox, TextField } from '@/shared/ui'

import { formatDuration } from '../lib/attendance-presentation'
import {
  FLEXIBLE_REQUIRED_CODE,
  requiredUnparsable,
  shownRequired,
  typedRequired,
  type FlexibleDraft,
} from '../lib/attendance-schedule-rules'

/**
 * «Erkin ish jadvali» belgisi va «Ish vaqti» maydoni — uchala HR formasida
 * (haftalik muharrir, yillik to'r paneli, jadval so'rovi) BITTA ko'rinish
 * (SPEC-erkin-jadval §7.1). Verifix formasidagidek: belgi qo'yilsa — bitta
 * qiymat, «7:30».
 *
 * Maydon sukutda eng kattasini (smena − tanaffus) ko'rsatadi va vaqtlar
 * o'zgarsa u bilan birga yuradi; qoidalari `attendance-schedule-rules.ts`
 * da (`FlexibleDraft`).
 *
 * Xato maydonning O'ZIDA va faqat kunning birinchi buzilgan qoidasi shu
 * bo'lsa (`code`, domen tartibi): matn `soat:daqiqa` bo'lmasa — «qanday
 * yozish», chegaradan chiqsa — server kodining tarjimasi. Chaqiruvchi shu
 * kodni kunning umumiy xato qatorida takrorlamaydi.
 *
 * Ostidagi izoh — ATAYLAB neytral: erkin kunda butun kunlik vaqt hisoblanadi
 * (egasi, 2026-10-20), boshlanish–tugash oralig'i faqat ma'lumot uchun.
 */
export function FlexibleDayFields({
  value,
  max,
  code,
  disabled,
  onChange,
  className,
}: {
  value: FlexibleDraft
  /** Shu kunning eng katta «Ish vaqti» — `maxRequiredMinutes`. */
  max: number
  /**
   * Kunning tekshiruv kodi (`validateScheduleDay`); faqat erkin kun kodi shu
   * yerda ko'rinadi. `null` — xato yo'q yoki hali ko'rsatilmaydi.
   */
  code: string | null
  disabled: boolean
  onChange: (next: FlexibleDraft) => void
  className?: string
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const error =
    code !== FLEXIBLE_REQUIRED_CODE
      ? undefined
      : requiredUnparsable(value)
        ? t('attendance.durationFormat')
        : errorMessage(code)

  return (
    <div className={cn('space-y-3', className)}>
      <Checkbox
        checked={value.isFlexible}
        disabled={disabled}
        onChange={(isFlexible) => onChange({ ...value, isFlexible })}
        label={t('attendance.flexible')}
      />

      {value.isFlexible && (
        <>
          {/*
            `inputMode` ATAYLAB yo'q: raqamli klaviaturada «:» belgisi yo'q va
            planshetda «7:30» ni yozib bo'lmasdi.
          */}
          <TextField
            className="max-w-[220px]"
            label={t('attendance.flexibleRequired')}
            placeholder="7:30"
            autoComplete="off"
            maxLength={5}
            value={shownRequired(value, max)}
            disabled={disabled}
            hint={t('attendance.flexibleRequiredHelper', { max: formatDuration(max) })}
            error={error}
            onChange={(event) => onChange(typedRequired(value, event.target.value, max))}
          />
          <p className="text-footnote text-[var(--fg-secondary)]">{t('attendance.flexibleHint')}</p>
        </>
      )}
    </div>
  )
}
