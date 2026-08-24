import type { LucideIcon } from 'lucide-react'
import { TrendingDown, TrendingUp } from 'lucide-react'
import type { ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'
import { Card } from './card'
import { IconTile, type IconTone } from './icon-tile'

/**
 * Asosiy ko'rsatkich kartasi.
 *
 * Ierarxiya ataylab: yorliq kichik va o'chiq → qiymat katta va qora →
 * izoh yana kichik. Ko'z birinchi raqamga tushadi.
 */
export function StatCard({
  label,
  value,
  hint,
  icon,
  tone = 'brand',
  delta,
  loading = false,
  className,
}: {
  label: string
  value: ReactNode
  hint?: ReactNode
  icon?: LucideIcon
  tone?: IconTone
  /** Musbat — o'sish (yashil), manfiy — pasayish (qizil). */
  delta?: { value: number; label: string }
  loading?: boolean
  className?: string
}) {
  if (loading) {
    return (
      <Card className={cn('p-5', className)}>
        <div className="animate-pulse space-y-3">
          <div className="h-3 w-24 rounded-full bg-[var(--bg-sunken)]" />
          <div className="h-8 w-32 rounded-md bg-[var(--bg-sunken)]" />
          <div className="h-3 w-40 rounded-full bg-[var(--bg-sunken)]" />
        </div>
      </Card>
    )
  }

  const rising = delta ? delta.value >= 0 : false
  const TrendIcon = rising ? TrendingUp : TrendingDown

  return (
    <Card className={cn('p-5', className)}>
      <div className="flex items-start justify-between gap-3">
        <p className="text-footnote text-[var(--fg-secondary)]">{label}</p>
        {icon && <IconTile icon={icon} tone={tone} />}
      </div>

      <p className="tabular mt-2 text-title1 leading-tight font-semibold tracking-tight">
        {value}
      </p>

      {delta && (
        <p
          className={cn(
            'mt-2 inline-flex items-center gap-1 text-caption1 font-medium',
            rising ? 'text-ios-green' : 'text-ios-red',
          )}
        >
          <TrendIcon className="size-3.5" strokeWidth={2.5} />
          <span className="tabular">
            {rising ? '+' : ''}
            {delta.value}%
          </span>
          <span className="font-normal text-[var(--fg-tertiary)]">{delta.label}</span>
        </p>
      )}

      {hint && !delta && (
        <p className="mt-2 text-caption1 text-[var(--fg-tertiary)]">{hint}</p>
      )}
    </Card>
  )
}
