import { useLocale } from '@/shared/i18n/use-locale'

import { formatPassTime } from '../lib/attendance-presentation'

/**
 * Oxirgi chiqish vaqti. Jadvalsiz kunda terminaldan o'tmay ketgan odam 22:00 da
 * avtomatik «ketdi» bo'ladi (egasining qarori, 2026-10-12) — u haqiqiy o'tish
 * emas, shuning uchun yonida «avto» izohi turadi. Belgini server beradi
 * (`leftAuto` / `autoExit`), 22:00 qoidasi klientda takrorlanmaydi.
 */
export function AttendanceExitTime({ value, auto = false }: { value: string | null; auto?: boolean }) {
  const { t } = useLocale()

  return (
    <>
      {formatPassTime(value)}
      {auto && value !== null && (
        <span
          className="ml-1 text-caption2 font-normal text-[var(--fg-tertiary)]"
          title={t('attendance.autoExitHint')}
        >
          {t('attendance.autoExit')}
        </span>
      )}
    </>
  )
}
