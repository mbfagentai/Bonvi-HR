import { useLocale } from '@/shared/i18n/use-locale'
import { Badge, Switch } from '@/shared/ui'

/**
 * Yozuvning faolligi.
 *
 * Ruxsat bo'lsa — almashtirgich: holatni ko'rsatadi va o'sha yerda
 * o'zgartiradi (iOS Sozlamalaridagi kabi bevosita boshqaruv).
 * Ruxsat bo'lmasa — oddiy yorliq.
 */
export function CatalogStatus({
  active,
  canManage,
  pending = false,
  onToggle,
}: {
  active: boolean
  canManage: boolean
  pending?: boolean
  onToggle: (next: boolean) => void
}) {
  const { t } = useLocale()
  const label = active ? t('catalog.statusActive') : t('catalog.statusInactive')

  if (!canManage) {
    return (
      <Badge tone={active ? 'success' : 'neutral'} dot>
        {label}
      </Badge>
    )
  }

  return (
    // Qator bosilganda tahrirlash oynasi ochiladi — almashtirgich bosilganda
    // esa faqat holat o'zgarishi kerak, shu sababli hodisa to'xtatiladi
    <span
      onClick={(event) => event.stopPropagation()}
      onKeyDown={(event) => event.stopPropagation()}
      className="inline-flex items-center gap-3"
    >
      <Switch
        checked={active}
        disabled={pending}
        onChange={onToggle}
        aria-label={active ? t('catalog.deactivate') : t('catalog.activate')}
      />
      <span className="hidden text-footnote text-[var(--fg-secondary)] lg:inline">
        {label}
      </span>
    </span>
  )
}
