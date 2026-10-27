import clsx from 'clsx'

/**
 * Xodim rasmi.
 *
 * Rasm BO'LSA — rasm ko'rinadi (loyihaning doimiy talabi). Rasmsiz
 * xodim esa haqiqatda ko'p: aynan ular uchun bu ilova yaratilgan —
 * yuz terminaliga yozib bo'lmaydigan odamlar. Shu sababli zaxira
 * ko'rinish ham to'liq huquqli: bosh harflar, tashlandiq nishoncha
 * emas.
 *
 * O'lchamlar SANOQLI (32, 40 va 64 px): Tailwind sinflari build paytida
 * topilishi kerak, runtime'da yasalgan sinf nomi tozalagichdan
 * o'tmaydi va uslubsiz qolardi. 40 px — jamoa ro'yxati qatori: boshliq
 * odamni YUZIDAN taniydi, 32 px da yuz ko'rinmaydi.
 */
type AvatarSize = 'sm' | 'md' | 'lg'

const BOX: Record<AvatarSize, string> = {
  sm: 'h-8 w-8',
  md: 'h-10 w-10',
  lg: 'h-16 w-16',
}

const TEXT: Record<AvatarSize, string> = {
  sm: 'text-footnote',
  md: 'text-subhead',
  lg: 'text-title3',
}

export function Avatar({
  photoUrl,
  fullName,
  size = 'sm',
  className,
}: {
  photoUrl: string | null
  fullName: string
  size?: AvatarSize
  className?: string
}) {
  const initials = fullName
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.slice(0, 1).toUpperCase())
    .join('')

  if (photoUrl) {
    return (
      <img
        src={photoUrl}
        alt=""
        className={clsx('rounded-pill object-cover', BOX[size], className)}
      />
    )
  }

  return (
    <span
      aria-hidden="true"
      className={clsx(
        'flex items-center justify-center rounded-pill bg-accent-soft text-accent-on-soft',
        BOX[size],
        className,
      )}
    >
      <span className={clsx('font-semibold', TEXT[size])}>{initials}</span>
    </span>
  )
}
