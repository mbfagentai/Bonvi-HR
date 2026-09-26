import { CircleAlert, CircleCheck, Info, TriangleAlert, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

export type AlertTone = 'danger' | 'warning' | 'info' | 'success'

/**
 * iOS'ning yondashuvi: ogohlantirish qichqirmaydi. Fon — o'sha rangning
 * juda suyultirilgan varianti, chegara yo'q, faqat ikon va matn to'yingan.
 * Shaffof fon tungi rejimda ham o'zi to'g'ri ishlaydi.
 */
const TONES: Record<AlertTone, { surface: string; accent: string; icon: LucideIcon }> = {
  danger: { surface: 'bg-ios-red/10', accent: 'text-ios-red', icon: CircleAlert },
  warning: { surface: 'bg-ios-orange/12', accent: 'text-ios-orange', icon: TriangleAlert },
  /*
     `info` yagona ohang bo'lib, u tizim tinti bilan bir xil rangda —
     shu sababli u xom `ios-blue` emas, tint tokenlarini ishlatadi:
     shunda ogohlantirish ko'ki tugma ko'ki bilan bir xil pog'onada turadi.
     Yon ta'siri foydali: `#007AFF` yorug' fonda 3.4:1 edi (AA dan past),
     `--accent-on-soft` esa 4.76:1 beradi. */
  info: {
    surface: 'bg-[var(--accent-soft)]',
    accent: 'text-[var(--accent-on-soft)]',
    icon: Info,
  },
  success: { surface: 'bg-ios-green/12', accent: 'text-ios-green', icon: CircleCheck },
}

/**
 * `title` berilsa — u asosiy qator, `children` esa uning ostidagi izoh.
 * Faqat `children` berilsa — u o'zi asosiy qator bo'ladi.
 */
export function Alert({
  tone = 'info',
  title,
  children,
  className,
}: {
  tone?: AlertTone
  title?: ReactNode
  children?: ReactNode
  className?: string
}) {
  const { surface, accent, icon: Icon } = TONES[tone]

  return (
    <div
      role="alert"
      className={cn('flex items-start gap-3 rounded-md p-3.5', surface, className)}
    >
      <Icon className={cn('mt-px size-[18px] shrink-0', accent)} strokeWidth={2.25} />

      <div className="min-w-0 flex-1 space-y-1">
        {title !== undefined && (
          <p className={cn('text-subhead font-semibold', accent)}>{title}</p>
        )}
        {/*
          `<div>`, `<p>` emas: izoh ichiga tugmali qator ham tushadi
          («Sinov muddati» ogohlantirishi) va `<p>` ichidagi `<div>`
          noto'g'ri HTML — React konsolga ogohlantirish yozardi.
        */}
        {children !== undefined && (
          <div
            className={cn(
              title === undefined
                ? cn('text-subhead font-medium', accent)
                : 'text-footnote text-[var(--fg-secondary)]',
            )}
          >
            {children}
          </div>
        )}
      </div>
    </div>
  )
}
