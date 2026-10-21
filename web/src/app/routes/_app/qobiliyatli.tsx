import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'
import {
  dateRangeToSearch,
  searchToDateRange,
  toSearchParam,
  validateDateRangeSearch,
  validateListSearch,
  type DateRangeSearch,
  type ListSearch,
} from '@/shared/lib/list-search'

/**
 * Qobiliyatli xodimlar — qo'shimcha vazifa berish mumkin bo'lganlar.
 *
 * `qora-royxat.tsx` ning aynan nusxasi: to'rtta belgi ro'yxati bitta
 * `FlagListPage` komponentidan yasaladi, farqi faqat `type` bilan.
 */
export const Route = createFileRoute('/_app/qobiliyatli')({
  /**
   * Qidiruv matni va belgi qo'yilgan sana oralig'i — manzil satrida
   * (loyihaning qat'iy konvensiyasi).
   * `FlagListPage` to'rtta marshrutga xizmat qiladi, shu sababli holatni
   * marshrut o'qiydi va sahifaga PROPS bilan uzatadi — sabab
   * `flag-list-page.tsx` izohida.
   */
  validateSearch: (search: Record<string, unknown>): ListSearch & DateRangeSearch => ({
    ...validateListSearch(search),
    ...validateDateRangeSearch(search),
  }),

  component: QobiliyatliSahifasi,
})

function QobiliyatliSahifasi() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()

  return (
    <FlagListPage
      type="capable"
      search={search.q ?? ''}
      // `replace` — qidiruvni aniqlashtirish alohida tarix qadami emas:
      // aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
      // Avvalgi manzil saqlanadi — qidiruv sana oralig'ini o'chirmasin.
      onSearchChange={(value) => {
        void navigate({
          search: (previous) => ({ ...previous, q: toSearchParam(value) }),
          replace: true,
        })
      }}
      range={searchToDateRange(search)}
      onRangeChange={(next) => {
        void navigate({
          search: (previous) => ({ ...previous, ...dateRangeToSearch(next) }),
          replace: true,
        })
      }}
    />
  )
}
