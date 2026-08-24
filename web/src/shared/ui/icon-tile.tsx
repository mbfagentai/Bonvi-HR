import { cva, type VariantProps } from 'class-variance-authority'
import type { LucideIcon } from 'lucide-react'

import { cn } from '@/shared/lib/cn'

/**
 * Yumshoq fonli rangli ikon — referenslardagi asosiy vizual element.
 * Fon pastel, ikon to'yingan. Rang ma'no tashiydi, bezak emas.
 */
const tile = cva(
  'grid shrink-0 place-items-center rounded-md transition-colors',
  {
    variants: {
      tone: {
        /*
          «brand» — tizim tinti (navy emas). IconTile ning standart ohangi
          shu bo'lgani uchun navy butun bosh sahifa bo'ylab tarqalgan edi;
          iOS HIG «Color» bitta tintni talab qiladi, navy esa endi faqat
          logotipda. `blue` ohangi ataylab qoldirildi: u ma'no tashiydi
          («axborot»), `brand` esa asosiy harakat bilan bog'lanadi.
        */
        brand:  'bg-[var(--accent-soft)] text-[var(--accent-on-soft)]',
        blue:   'bg-ios-blue/10 text-ios-blue',
        green:  'bg-ios-green/12 text-ios-green',
        red:    'bg-ios-red/10 text-ios-red',
        orange: 'bg-ios-orange/12 text-ios-orange',
        purple: 'bg-ios-purple/10 text-ios-purple',
        teal:   'bg-ios-teal/12 text-ios-teal',
        gray:   'bg-[var(--bg-sunken)] text-[var(--fg-secondary)]',
      },
      size: {
        sm: 'size-8 [&>svg]:size-4',
        md: 'size-10 [&>svg]:size-[18px]',
        lg: 'size-12 [&>svg]:size-5',
      },
    },
    defaultVariants: { tone: 'brand', size: 'md' },
  },
)

export type IconTone = NonNullable<VariantProps<typeof tile>['tone']>

export function IconTile({
  icon: Icon,
  tone,
  size,
  className,
}: VariantProps<typeof tile> & { icon: LucideIcon; className?: string }) {
  return (
    <span className={cn(tile({ tone, size }), className)}>
      <Icon strokeWidth={2} />
    </span>
  )
}
