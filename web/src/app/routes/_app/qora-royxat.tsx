import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'
import { toSearchParam, validateListSearch } from '@/shared/lib/list-search'

/**
 * Qora ro'yxat — qayta ishga olinmaydiganlar.
 *
 * HR aniq so'ragan qoida shu ro'yxatga tayanadi: bo'shatishda va qayta
 * ishga olishda tizim ogohlantiradi. Aynan shu sababli bu yerda qidiruv
 * eng muhim: «bu odam qora ro'yxatdami?» degan savolga JSHSHIR bilan
 * javob berish kerak, ismning yozilishiga qarab emas.
 */
export const Route = createFileRoute('/_app/qora-royxat')({
  /**
   * Qidiruv matni — manzil satrida (loyihaning qat'iy konvensiyasi).
   * `FlagListPage` to'rtta marshrutga xizmat qiladi, shu sababli holatni
   * marshrut o'qiydi va sahifaga PROPS bilan uzatadi — sabab
   * `flag-list-page.tsx` izohida.
   */
  validateSearch: validateListSearch,

  component: QoraRoyxatSahifasi,
})

function QoraRoyxatSahifasi() {
  const { q } = Route.useSearch()
  const navigate = Route.useNavigate()

  return (
    <FlagListPage
      type="blacklist"
      search={q ?? ''}
      // `replace` — qidiruvni aniqlashtirish alohida tarix qadami emas:
      // aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
      onSearchChange={(value) => {
        void navigate({ search: { q: toSearchParam(value) }, replace: true })
      }}
    />
  )
}
