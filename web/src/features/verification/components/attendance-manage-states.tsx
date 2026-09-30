import { CloudOff, ShieldOff, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Button, Card, EmptyState, IconTile, PageHeader } from '@/shared/ui'

/**
 * Boshqaruv sahifalarining (moslashtirish, jadvallar, qurilmalar) uchta
 * umumiy holati — bir joyda.
 *
 * NEGA alohida fayl: uchala sahifa ham «ruxsat yo'q» va «yuklab
 * bo'lmadi» degan bir xil ikki gapni aytadi. Har birida o'z nusxasi
 * bo'lsa, biri tuzatilganda (masalan qayta urinish tugmasi qo'shilsa)
 * qolgan ikkitasi jimgina eskirardi.
 */

/**
 * Ruxsat yo'q — sahifa sarlavhasi bilan birga.
 *
 * Marshrutda `beforeLoad` to'sig'i YO'Q (SPEC §8.2, `hodisalar.tsx`
 * naqshi): server baribir 403 qaytaradi, sahifa esa buni oq ekran emas,
 * tushunarli gap bilan aytadi. So'rovlar esa umuman yuborilmaydi
 * (`enabled`) — konsol 403 bilan to'lmasin.
 */
export function AttendanceNoAccess({ icon, title }: { icon: LucideIcon; title: string }) {
  const { t } = useLocale()

  return (
    <div className="space-y-6">
      <PageHeader title={<AttendancePageTitle icon={icon} title={title} />} />
      <Card>
        <EmptyState
          icon={ShieldOff}
          title={t('attendance.noAccessTitle')}
          description={t('attendance.noAccessText')}
        />
      </Card>
    </div>
  )
}

/** Sahifa sarlavhasi — ikonka plitkasi bilan (`incidents-page.tsx` naqshi). */
export function AttendancePageTitle({ icon, title }: { icon: LucideIcon; title: ReactNode }) {
  return (
    <span className="flex items-center gap-3">
      <IconTile icon={icon} tone="brand" size="lg" />
      {title}
    </span>
  )
}

/**
 * «Yuklab bo'lmadi» — server kodi tarjimasi va qayta urinish.
 *
 * ⚠️ Bo'sh holat bilan ARALASHTIRILMAYDI: «qurilma yo'q» va «qurilmalarni
 * o'qib bo'lmadi» — ikki xil gap. Birinchisi «qurilma qo'shing» deydi,
 * ikkinchisi «tarmoqni tekshiring». Xato bo'sh ro'yxat bo'lib ko'rinsa,
 * HR mavjud terminalni ikkinchi marta ro'yxatga olishga urinardi.
 */
export function AttendanceLoadError({
  error,
  onRetry,
  retrying = false,
  className,
}: {
  error: unknown
  onRetry: () => void
  retrying?: boolean
  className?: string
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  return (
    <EmptyState
      className={className}
      icon={CloudOff}
      title={t('common.error')}
      description={errorMessage(problemCode(error))}
      action={
        <Button type="button" onClick={onRetry} disabled={retrying}>
          {t('common.retry')}
        </Button>
      }
    />
  )
}
