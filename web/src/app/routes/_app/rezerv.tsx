import { createFileRoute } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'
import { FlagListPage } from '@/features/flags/components/flag-list-page'
import { RECRUITING_VIEW, ReserveCandidatesPage } from '@/features/recruiting'
import { useLocale } from '@/shared/i18n/use-locale'
import { toSearchParam, validateListSearch, type ListSearch } from '@/shared/lib/list-search'
import { SegmentedControl } from '@/shared/ui'

type RezervTab = 'candidates' | 'employees'

interface RezervSearch extends ListSearch {
  /** Sukut — nomzodlar; manzilda faqat «employees» yoziladi. */
  tab?: 'employees'
  /** Nomzodlar sahifasi, 1 dan. Birinchi sahifa manzilga yozilmaydi. */
  page?: number
}

/**
 * Rezerv — hozir olinmagan, lekin kerak bo'lsa chaqirsa bo'ladigan odamlar.
 *
 * ⭐ Ikki bo'lim (foydalanuvchi qarori, 2026-09-26): rezervga o'tkazilgan
 * NOMZODLAR (nomzodlar ro'yxatidan shu yerga ko'chdi) va rezerv belgisi
 * qo'yilgan XODIMLAR. Nomzodlar bo'limi faqat `recruiting.view` ruxsati
 * borlarga ko'rinadi — busiz sahifa avvalgidek faqat xodimlarni ko'rsatadi.
 *
 * Xodimlar ro'yxatining o'zi `FlagListPage` da: to'rttala belgi sahifasi
 * bir xil ishlaydi va faqat turi bilan farq qiladi.
 */
export const Route = createFileRoute('/_app/rezerv')({
  /**
   * Qidiruv matni, bo'lim va sahifa — manzil satrida (loyihaning qat'iy
   * konvensiyasi). Holatni marshrut o'qiydi va sahifalarga PROPS bilan
   * uzatadi — sabab `flag-list-page.tsx` izohida.
   */
  validateSearch: (search: Record<string, unknown>): RezervSearch => {
    const page = Number(search['page'])

    return {
      ...validateListSearch(search),
      tab: search['tab'] === 'employees' ? 'employees' : undefined,
      page: Number.isInteger(page) && page > 1 ? page : undefined,
    }
  },

  component: RezervSahifasi,
})

function RezervSahifasi() {
  const { q, tab, page } = Route.useSearch()
  const navigate = Route.useNavigate()
  const { t } = useLocale()
  const { hasPermission } = useAuth()

  const canViewCandidates = hasPermission(RECRUITING_VIEW)
  const current: RezervTab = canViewCandidates && tab !== 'employees' ? 'candidates' : 'employees'

  // `replace` — qidiruvni aniqlashtirish alohida tarix qadami emas:
  // aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
  // Yangi qidiruv sahifani birinchisiga qaytaradi.
  const changeSearch = (value: string) => {
    void navigate({
      search: (previous) => ({ ...previous, q: toSearchParam(value), page: undefined }),
      replace: true,
    })
  }

  const tabs = canViewCandidates ? (
    <SegmentedControl<RezervTab>
      aria-label={t('flags.page.reserve.title')}
      options={[
        { value: 'candidates', label: t('recruiting.reserveSection.candidates') },
        { value: 'employees', label: t('recruiting.reserveSection.employees') },
      ]}
      value={current}
      onChange={(next) => {
        void navigate({
          search: (previous) => ({
            ...previous,
            tab: next === 'employees' ? 'employees' : undefined,
            page: undefined,
          }),
        })
      }}
    />
  ) : undefined

  if (current === 'candidates') {
    return (
      <ReserveCandidatesPage
        search={q ?? ''}
        onSearchChange={changeSearch}
        page={page ?? 1}
        onPageChange={(next) => {
          void navigate({
            search: (previous) => ({ ...previous, page: next > 1 ? next : undefined }),
          })
        }}
        tabs={tabs}
      />
    )
  }

  return (
    <FlagListPage type="reserve" search={q ?? ''} onSearchChange={changeSearch} tabs={tabs} />
  )
}
