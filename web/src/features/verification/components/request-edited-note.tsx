import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

/**
 * «Ilovada o'zgartirilgan · 20.10.2026 14:05» — kutayotgan so'rov yuborilgandan
 * keyin tahrirlangan (SPEC-sorov-tahriri §3.5, §7.2).
 *
 * <b>Nega kerak.</b> Xodim so'rovini qaror qabul qilinguncha ilovadan
 * o'zgartira oladi va bu haqda alohida bildirishnoma YO'Q — HR buni navbat
 * qatorida va qaror oynasida shu yozuvdan biladi. Kim o'zgartirgani ikki xil:
 * xodimning o'zi (ilova) yoki HR (`editedByEmployee: false` — shunchaki
 * «O'zgartirilgan»); to'liq tarix auditda.
 *
 * Vaqt Toshkent bo'yicha (`dateTime`): bir kunda ikki marta tahrirlangan
 * so'rovda sananing o'zi yetmaydi.
 *
 * Bitta komponent to'rt joyga (ikki navbat, ikki qaror oynasi): matn va
 * qoida bitta bo'lsin, biri o'zgarib, ikkinchisi eskirib qolmasin.
 */
export function RequestEditedNote({
  request,
  className,
}: {
  request: { editedAt?: string | null; editedByEmployee?: boolean }
  className?: string
}) {
  const { t, dateTime } = useLocale()

  if (request.editedAt === undefined || request.editedAt === null) return null

  return (
    <span className={cn('block text-caption2 text-[var(--fg-tertiary)]', className)}>
      {t(
        request.editedByEmployee === true
          ? 'attendance.requestEditedInApp'
          : 'attendance.requestEditedAt',
        { date: dateTime(request.editedAt) },
      )}
    </span>
  )
}
