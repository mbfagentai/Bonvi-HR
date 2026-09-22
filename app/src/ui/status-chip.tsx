import clsx from 'clsx'
import { useTranslation } from 'react-i18next'

import type { StatusLook } from '@/data/status-look'

/**
 * Holat chipi (UX §E.4): nuqta + tint + SO'Z.
 *
 * ⚠️ Rang hech qachon ma'noning yagona tashuvchisi emas — so'z doimo
 * bor va aynan u tarjima qilinadi (UX §D.1, 6-nuqson). Matn `--fg`
 * rangida: shu qoida barcha tintlarda AA ni kafolatlaydi.
 */
export function StatusChip({
  look,
  size = 'md',
}: {
  look: StatusLook
  size?: 'sm' | 'md'
}) {
  const { t } = useTranslation()

  return (
    <span
      className={clsx(
        'inline-flex max-w-full items-center rounded-chip text-fg',
        look.tintClass,
        size === 'sm' ? 'px-2 py-0.5 text-caption1' : 'px-2.5 py-1 text-footnote',
      )}
    >
      {look.dotClass ? (
        <span className={clsx('mr-1.5 h-2 w-2 shrink-0 rounded-pill', look.dotClass)} />
      ) : null}
      <span className="truncate">{t(look.labelKey)}</span>
    </span>
  )
}
