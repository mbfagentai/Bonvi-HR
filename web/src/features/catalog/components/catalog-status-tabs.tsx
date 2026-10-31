import { useLocale } from '@/shared/i18n/use-locale'
import { SegmentedControl } from '@/shared/ui'

export type CatalogStatusTab = 'active' | 'inactive'

/**
 * «Faol» / «Faol emas» — har ma'lumotnoma menyusining tepasida (egasi, 2026-10-31).
 *
 * Menyu har doim «Faol» da ochiladi: kundalik ish faqat faol yozuvlar bilan. Faol emaslari
 * yo'qolmaydi — alohida tabda turadi va o'sha yerdan qayta yoqiladi.
 */
export function CatalogStatusTabs({
  value,
  onChange,
  activeCount,
  inactiveCount,
}: {
  value: CatalogStatusTab
  onChange: (value: CatalogStatusTab) => void
  activeCount: number | null
  inactiveCount: number | null
}) {
  const { t, num } = useLocale()
  const label = (text: string, count: number | null) => (count === null ? text : `${text} · ${num(count)}`)

  return (
    <SegmentedControl<CatalogStatusTab>
      value={value}
      onChange={onChange}
      aria-label={t('catalog.statusTabs')}
      options={[
        { value: 'active', label: label(t('catalog.tabActive'), activeCount) },
        { value: 'inactive', label: label(t('catalog.tabInactive'), inactiveCount) },
      ]}
    />
  )
}
