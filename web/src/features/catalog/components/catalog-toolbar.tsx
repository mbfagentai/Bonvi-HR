import { Search } from 'lucide-react'
import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Input, SegmentedControl } from '@/shared/ui'

/**
 * Ro'yxat ustidagi boshqaruv qatori.
 *
 * Qidiruv butun bo'sh kenglikni egallaydi — u eng ko'p ishlatiladigan
 * element. Faollik filtri o'ngda, chunki u kamdan-kam o'zgartiriladi.
 */
export function CatalogToolbar({
  query,
  onQueryChange,
  onlyActive,
  onOnlyActiveChange,
  trailing,
}: {
  query: string
  onQueryChange: (value: string) => void
  onlyActive: boolean
  onOnlyActiveChange: (value: boolean) => void
  /** Sahifaga xos qo'shimcha tugmalar (masalan «Hammasini yopish»). */
  trailing?: ReactNode
}) {
  const { t } = useLocale()

  return (
    <div className="flex flex-wrap items-center gap-3 border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
      <div className="min-w-[200px] flex-1">
        <Input
          icon={Search}
          shape="pill"
          type="search"
          value={query}
          onChange={(event) => onQueryChange(event.target.value)}
          placeholder={t('catalog.searchPlaceholder')}
          aria-label={t('common.search')}
        />
      </div>

      <SegmentedControl
        value={onlyActive ? 'active' : 'all'}
        onChange={(value) => onOnlyActiveChange(value === 'active')}
        aria-label={t('catalog.filterLabel')}
        options={[
          { value: 'active', label: t('catalog.onlyActive') },
          { value: 'all', label: t('common.all') },
        ]}
      />

      {trailing}
    </div>
  )
}
