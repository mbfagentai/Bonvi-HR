import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'
import { toSearchParam, validateListSearch } from '@/shared/lib/list-search'

/**
 * Rezerv — hozir olinmagan, lekin kerak bo'lsa chaqirsa bo'ladigan odamlar.
 *
 * Ro'yxatning o'zi `FlagListPage` da: to'rttala belgi sahifasi bir xil
 * ishlaydi va faqat turi bilan farq qiladi.
 */
export const Route = createFileRoute('/_app/rezerv')({
  /**
   * Qidiruv matni — manzil satrida (loyihaning qat'iy konvensiyasi).
   * `FlagListPage` to'rtta marshrutga xizmat qiladi, shu sababli holatni
   * marshrut o'qiydi va sahifaga PROPS bilan uzatadi — sabab
   * `flag-list-page.tsx` izohida.
   */
  validateSearch: validateListSearch,

  component: RezervSahifasi,
})

function RezervSahifasi() {
  const { q } = Route.useSearch()
  const navigate = Route.useNavigate()

  return (
    <FlagListPage
      type="reserve"
      search={q ?? ''}
      // `replace` — qidiruvni aniqlashtirish alohida tarix qadami emas:
      // aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
      onSearchChange={(value) => {
        void navigate({ search: { q: toSearchParam(value) }, replace: true })
      }}
    />
  )
}
