import type { LucideIcon } from 'lucide-react'

import { cn } from '@/shared/lib/cn'

/**
 * Xavfli amal — iOS «Settings» dagi kabi QATOR, tugma emas.
 *
 * NEGA to'ldirilgan qizil tugma emas: iOS HIG bo'yicha to'ldirilgan rang
 * ekrandagi ASOSIY amalni belgilaydi. «Chiqish» yoki «Rasmni o'chirish»
 * esa sozlamalar oynasining maqsadi emas — u shunchaki mavjud imkoniyat.
 * Katta qizil blok butun oynani o'ziga tortadi va bosim hissi beradi;
 * qizil MATN esa xavfni bir xilda, lekin xotirjam aytadi.
 *
 * Rang `--danger-text` dan olinadi (`--danger` emas): u to'ldirilgan FON
 * uchun va uning ustida oq matn turadi. Matn ko'rinishi uchun kontrast
 * alohida hisoblangan — ikkala rejimda ham AA (4.5:1) dan yuqori.
 */
export function DangerRow({
  icon: Icon,
  label,
  onClick,
  disabled = false,
}: {
  icon: LucideIcon
  label: string
  onClick: () => void
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'press flex w-full items-center gap-3 px-4 py-3 text-left',
        'text-subhead font-medium text-[var(--danger-text)]',
        'hover:bg-[var(--bg-hover)] active:bg-[var(--bg-active)]',
        'disabled:pointer-events-none disabled:opacity-50',
      )}
    >
      <Icon className="size-[18px] shrink-0" strokeWidth={2} />
      <span className="truncate">{label}</span>
    </button>
  )
}
