import { Search } from 'lucide-react'
import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Input } from '@/shared/ui'

/**
 * Ro'yxat ustidagi boshqaruv qatori.
 *
 * Qidiruv butun bo'sh kenglikni egallaydi — u yagona doimiy ehtiyoj.
 *
 * «Faqat faollar / Hammasi» filtri OLIB TASHLANDI: ro'yxat endi hamma
 * yozuvni ko'rsatadi, faol emaslari nomi yonidagi yorliq bilan ajralib
 * turadi. Filtr HR ni ikki xil ro'yxat orasida yurishga majbur qilardi va
 * «yozuv yo'qolib qoldi» degan tuyg'u tug'dirardi.
 */
export function CatalogToolbar({
  query,
  onQueryChange,
  trailing,
}: {
  query: string
  onQueryChange: (value: string) => void
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

      {trailing}
    </div>
  )
}
