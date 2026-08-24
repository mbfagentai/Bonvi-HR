import { cva, type VariantProps } from 'class-variance-authority'
import type { ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

const badge = cva(
  'inline-flex items-center gap-1.5 rounded-full font-medium whitespace-nowrap',
  {
    variants: {
      tone: {
        neutral: 'bg-[var(--bg-sunken)] text-[var(--fg-secondary)]',
        /*
          «brand» ohangi endi brend navy'sini emas, tizim tintini ko'rsatadi.
          iOS HIG «Color»: interfeysda bitta tint bo'ladi; navy faqat
          logotipda qoldi, shu sababli yorliqdagi navy tint bilan
          urishib qolar edi. Tokenlar orqali tungi rejim o'z-o'zidan to'g'ri.
        */
        brand:   'bg-[var(--accent-soft)] text-[var(--accent-on-soft)]',
        success: 'bg-ios-green/12 text-ios-green',
        warning: 'bg-ios-orange/14 text-ios-orange',
        danger:  'bg-ios-red/10 text-ios-red',
        info:    'bg-ios-blue/10 text-ios-blue',
        purple:  'bg-ios-purple/10 text-ios-purple',
      },
      size: {
        sm: 'h-5  px-2   text-caption2',
        md: 'h-6  px-2.5 text-caption1',
      },
    },
    defaultVariants: { tone: 'neutral', size: 'md' },
  },
)

export type BadgeTone = NonNullable<VariantProps<typeof badge>['tone']>

/**
 * Holat yorlig'i. `dot` bilan — oldida kichik rangli nuqta
 * (referenslardagi "ACCEPTED · PENDING" uslubi).
 */
export function Badge({
  children,
  tone,
  size,
  dot = false,
  className,
  title,
}: VariantProps<typeof badge> & {
  children: ReactNode
  dot?: boolean
  className?: string
  /** Kursor ustiga kelganda chiqadigan izoh — yorliq qisqa bo'lgani uchun foydali. */
  title?: string
}) {
  return (
    <span className={cn(badge({ tone, size }), className)} title={title}>
      {dot && <span className="size-1.5 rounded-full bg-current" />}
      {children}
    </span>
  )
}
