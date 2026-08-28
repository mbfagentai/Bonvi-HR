import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'
import { toSearchParam, validateListSearch } from '@/shared/lib/list-search'

/**
 * Qobiliyatli xodimlar — qo'shimcha vazifa berish mumkin bo'lganlar.
 *
 * `qora-royxat.tsx` ning aynan nusxasi: to'rtta belgi ro'yxati bitta
 * `FlagListPage` komponentidan yasaladi, farqi faqat `type` bilan.
 */
export const Route = createFileRoute('/_app/qobiliyatli')({
  /**
   * Qidiruv matni — manzil satrida (loyihaning qat'iy konvensiyasi).
   * `FlagListPage` to'rtta marshrutga xizmat qiladi, shu sababli holatni
   * marshrut o'qiydi va sahifaga PROPS bilan uzatadi — sabab
   * `flag-list-page.tsx` izohida.
   */
  validateSearch: validateListSearch,

  component: QobiliyatliSahifasi,
})

function QobiliyatliSahifasi() {
  const { q } = Route.useSearch()
  const navigate = Route.useNavigate()

  return (
    <FlagListPage
      type="capable"
      search={q ?? ''}
      // `replace` — qidiruvni aniqlashtirish alohida tarix qadami emas:
      // aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
      onSearchChange={(value) => {
        void navigate({ search: { q: toSearchParam(value) }, replace: true })
      }}
    />
  )
}
