import { cn } from '@/shared/lib/cn'
import { DateField } from './date-field'

/**
 * Sana ORALIG'I — bitta yorliq, ostida ikki maydon yonma-yon: «sanadan – sanagacha».
 *
 * Egasi 2026-10-22: filtr panelida ikki sana ikki katakni egallab, panelni
 * cho'zib yuborardi — «bittasining o'rniga ikkitasi sig'sin», va bu HAMMA
 * ro'yxatda bir xil bo'lsin. Shu sababli oraliq har joyda shu komponent orqali.
 *
 * Har bir maydonning o'z (ko'rinmas) yorlig'i bor — skrinrider uchun
 * «Ishga kirgan sanadan» / «…sanagacha». Chegaralar bir-birini taqvimda
 * cheklaydi; qo'lda terilgan teskari oraliqni sahifa o'zi ushlaydi.
 */
export function DateRangeField({
  label,
  fromLabel,
  toLabel,
  from,
  to,
  onFromChange,
  onToChange,
  max,
  size = 'sm',
  className,
}: {
  /** Ko'rinadigan umumiy yorliq: «Ishga kirgan sana». */
  label: string
  fromLabel: string
  toLabel: string
  /** ISO sana yoki `''`. */
  from: string
  to: string
  onFromChange: (value: string) => void
  onToChange: (value: string) => void
  /** Umumiy yuqori chegara (masalan bugun) — ikkala maydonga ham. */
  max?: string
  size?: 'sm' | 'md'
  className?: string
}) {
  return (
    <div role="group" aria-label={label} className={cn(size === 'sm' ? 'space-y-1.5' : 'space-y-2', className)}>
      <span className="block text-footnote font-medium text-[var(--fg-secondary)]">{label}</span>

      <div className="flex items-center gap-2">
        <DateField
          ariaLabel={fromLabel}
          size={size}
          value={from}
          max={to === '' ? max : to}
          onChange={onFromChange}
          className="min-w-0 flex-1"
        />
        <span aria-hidden className="shrink-0 text-[var(--fg-tertiary)]">
          –
        </span>
        <DateField
          ariaLabel={toLabel}
          size={size}
          value={to}
          min={from === '' ? undefined : from}
          max={max}
          onChange={onToChange}
          className="min-w-0 flex-1"
        />
      </div>
    </div>
  )
}
