import { TriangleAlert } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Badge } from '@/shared/ui'
import type { EmployeeFlagType, EmployeeStatus } from '../api/employees-api'
import { FLAG_LOOK, STATUS_KEY, STATUS_TONE } from '../lib/employee-presentation'

/** Mehnat munosabatining holati: ishlayapti / sinovda / ketgan. */
export function StatusBadge({
  status,
  size = 'md',
}: {
  status: EmployeeStatus
  size?: 'sm' | 'md'
}) {
  const { t } = useLocale()

  return (
    <Badge tone={STATUS_TONE[status]} size={size} dot>
      {t(STATUS_KEY[status])}
    </Badge>
  )
}

/** Belgi yorlig'i: rezerv / qora ro'yxat / nazorat / qobiliyatli. */
export function FlagBadge({
  type,
  size = 'md',
}: {
  type: EmployeeFlagType
  size?: 'sm' | 'md'
}) {
  const { t } = useLocale()
  const look = FLAG_LOOK[type]

  return (
    <Badge tone={look.tone} size={size} dot>
      {t(`employees.flags.${look.key}`)}
    </Badge>
  )
}

/**
 * «Tekshirish kerak» belgisi.
 *
 * Bu yozuv Excel'dan ko'chirilgan va to'liq tekshiruvdan o'tmagan:
 * JSHSHIR nazorat raqami xato, sana mos kelmagan yoki maydon bo'sh
 * bo'lishi mumkin. Belgi ataylab sariq va e'tiborni tortadi — HR uni
 * ko'rib, ma'lumotni asl hujjat bilan solishtirishi kerak.
 *
 * `compact` — ro'yxatdagi kichik ko'rinish (faqat uchburchak).
 */
export function NeedsReviewMark({
  note,
  compact = false,
  className,
}: {
  note?: string | null
  compact?: boolean
  className?: string
}) {
  const { t } = useLocale()
  const label = t('employees.needsReview')
  const title = note !== null && note !== undefined && note !== '' ? `${label}: ${note}` : label

  if (compact) {
    return (
      <span
        title={title}
        aria-label={title}
        className={cn(
          'inline-grid size-5 shrink-0 place-items-center rounded-full',
          'bg-ios-orange/14 text-ios-orange',
          className,
        )}
      >
        <TriangleAlert className="size-3" strokeWidth={2.5} />
      </span>
    )
  }

  return (
    // `title` o'ram elementida: sabab matni («nima tekshirilishi kerak»)
    // kursor ustiga kelganda chiqadi, lekin yorliqning o'zini uzaytirmaydi
    <span title={title} className={cn('inline-flex', className)}>
      <Badge tone="warning">
        <TriangleAlert className="size-3.5" strokeWidth={2.5} />
        {label}
      </Badge>
    </span>
  )
}

/**
 * Yo'nalish yorliqlari («Вело, метан, шина, логистика»).
 *
 * Rang yo'nalish nomidan barqaror hosil qilinadi: bitta yo'nalish butun
 * tizimda doim bir xil rangda ko'rinadi va HR ularni o'qimasdan tanib
 * oladi. Ma'lumotnomada rang maydoni paydo bo'lsa — shu funksiya
 * o'sha qiymatga o'tadi.
 */
const DIRECTION_TONES = ['info', 'purple', 'success', 'warning', 'brand', 'danger'] as const

export function DirectionBadge({ name, size = 'sm' }: { name: string; size?: 'sm' | 'md' }) {
  let hash = 0
  for (const ch of name) hash = (hash * 31 + ch.codePointAt(0)!) >>> 0

  return (
    <Badge tone={DIRECTION_TONES[hash % DIRECTION_TONES.length]} size={size}>
      {name}
    </Badge>
  )
}
