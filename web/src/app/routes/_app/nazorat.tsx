import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'
import { toSearchParam, validateListSearch } from '@/shared/lib/list-search'

/** Nazoratdagilar — bir muddat kuzatiladigan xodimlar. */
export const Route = createFileRoute('/_app/nazorat')({
  /**
   * Qidiruv matni — manzil satrida (loyihaning qat'iy konvensiyasi).
   * `FlagListPage` to'rtta marshrutga xizmat qiladi, shu sababli holatni
   * marshrut o'qiydi va sahifaga PROPS bilan uzatadi — sabab
   * `flag-list-page.tsx` izohida.
   */
  validateSearch: validateListSearch,

  component: NazoratSahifasi,
})

function NazoratSahifasi() {
  const { q } = Route.useSearch()
  const navigate = Route.useNavigate()

  return (
    <FlagListPage
      type="monitoring"
      search={q ?? ''}
      // `replace` — qidiruvni aniqlashtirish alohida tarix qadami emas:
      // aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
      onSearchChange={(value) => {
        void navigate({ search: { q: toSearchParam(value) }, replace: true })
      }}
    />
  )
}
