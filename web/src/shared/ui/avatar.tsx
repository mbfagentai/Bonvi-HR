import { cn } from '@/shared/lib/cn'

/** Ism-familiyadan bosh harflarni oladi: «Мамажонов Бобуржон» → «МБ» */
function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

/**
 * Rasm bo'lmasa — ismdan hosil qilingan barqaror rang.
 * Bir xil ism doim bir xil rangda ko'rinadi.
 */
const PALETTE = [
  'bg-ios-blue/12 text-ios-blue',
  'bg-ios-green/14 text-ios-green',
  'bg-ios-orange/14 text-ios-orange',
  'bg-ios-purple/12 text-ios-purple',
  'bg-ios-teal/14 text-ios-teal',
  'bg-ios-pink/12 text-ios-pink',
  'bg-ios-indigo/12 text-ios-indigo',
]

function toneFor(name: string): string {
  let hash = 0
  for (const ch of name) hash = (hash * 31 + ch.codePointAt(0)!) >>> 0
  return PALETTE[hash % PALETTE.length]!
}

const SIZES = {
  sm: 'size-8 text-caption1',
  md: 'size-10 text-footnote',
  lg: 'size-12 text-subhead',
  xl: 'size-20 text-title3',
} as const

/** O'lchamni tashqariga chiqarish kerak: uni o'rovchi komponentlar uzatadi. */
export type AvatarSize = keyof typeof SIZES

export function Avatar({
  name,
  src,
  size = 'md',
  className,
}: {
  name: string
  src?: string | null
  size?: AvatarSize
  className?: string
}) {
  if (src) {
    return (
      <img
        src={src}
        alt={name}
        loading="lazy"
        className={cn(
          'shrink-0 rounded-full border border-[var(--border)] object-cover',
          SIZES[size],
          className,
        )}
      />
    )
  }

  return (
    <span
      aria-label={name}
      title={name}
      className={cn(
        'grid shrink-0 place-items-center rounded-full font-semibold select-none',
        SIZES[size],
        toneFor(name),
        className,
      )}
    >
      {initials(name)}
    </span>
  )
}
