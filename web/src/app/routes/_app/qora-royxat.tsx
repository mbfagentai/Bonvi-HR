import { createFileRoute } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'
import { FlagListPage } from '@/features/flags/components/flag-list-page'
import { BlacklistCandidatesPage, RECRUITING_VIEW } from '@/features/recruiting'
import { useLocale } from '@/shared/i18n/use-locale'
import {
  dateRangeToSearch,
  searchToDateRange,
  toSearchParam,
  validateDateRangeSearch,
  validateListSearch,
  type DateRange,
  type DateRangeSearch,
  type ListSearch,
} from '@/shared/lib/list-search'
import { SegmentedControl } from '@/shared/ui'

type QoraRoyxatTab = 'candidates' | 'employees'

interface QoraRoyxatSearch extends ListSearch, DateRangeSearch {
  /** Sukut — nomzodlar; manzilda faqat «employees» yoziladi. */
  tab?: 'employees'
  /** Nomzodlar sahifasi, 1 dan. Birinchi sahifa manzilga yozilmaydi. */
  page?: number
}

/**
 * Qora ro'yxat — qayta olinmaydiganlar.
 *
 * HR aniq so'ragan qoida shu ro'yxatga tayanadi: bo'shatishda va qayta
 * ishga olishda tizim ogohlantiradi. Aynan shu sababli bu yerda qidiruv
 * eng muhim: «bu odam qora ro'yxatdami?» degan savolga JSHSHIR bilan
 * javob berish kerak, ismning yozilishiga qarab emas.
 *
 * ⭐ Ikki bo'lim (egasining qarori, 2026-10-07: «xuddi rezerv bo'limi
 * kabi»): qora ro'yxatdagi NOMZODLAR va qora ro'yxat belgisi qo'yilgan
 * XODIMLAR. Tuzilishi `rezerv.tsx` bilan aynan bir xil: nomzodlar bo'limi
 * faqat `recruiting.view` ruxsati borlarga ko'rinadi — busiz sahifa
 * avvalgidek faqat xodimlarni ko'rsatadi.
 */
export const Route = createFileRoute('/_app/qora-royxat')({
  /**
   * Qidiruv matni, sana oralig'i, bo'lim va sahifa — manzil satrida
   * (loyihaning qat'iy konvensiyasi). Holatni marshrut o'qiydi va
   * sahifalarga PROPS bilan uzatadi — sabab `flag-list-page.tsx` izohida.
   */
  validateSearch: (search: Record<string, unknown>): QoraRoyxatSearch => {
    const page = Number(search['page'])

    return {
      ...validateListSearch(search),
      ...validateDateRangeSearch(search),
      tab: search['tab'] === 'employees' ? 'employees' : undefined,
      page: Number.isInteger(page) && page > 1 ? page : undefined,
    }
  },

  component: QoraRoyxatSahifasi,
})

function QoraRoyxatSahifasi() {
  const { q, tab, page, from, to } = Route.useSearch()
  const navigate = Route.useNavigate()
  const { t } = useLocale()
  const { hasPermission } = useAuth()

  const canViewCandidates = hasPermission(RECRUITING_VIEW)
  const current: QoraRoyxatTab =
    canViewCandidates && tab !== 'employees' ? 'candidates' : 'employees'

  // `replace` — qidiruvni aniqlashtirish alohida tarix qadami emas:
  // aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
  // Yangi qidiruv sahifani birinchisiga qaytaradi.
  const changeSearch = (value: string) => {
    void navigate({
      search: (previous) => ({ ...previous, q: toSearchParam(value), page: undefined }),
      replace: true,
    })
  }

  /*
    Sana oralig'i IKKALA bo'limga umumiy (qidiruv kabi), lekin ma'nosi
    bo'limga qarab: nomzodlarda — ariza kelgan kun, xodimlarda — belgi
    qo'yilgan kun. Yorliq har bo'limda o'z ma'nosini aytadi. Oraliq
    o'zgarsa nomzodlar sahifasi birinchisiga qaytadi.
  */
  const range = searchToDateRange({ from, to })
  const changeRange = (next: DateRange) => {
    void navigate({
      search: (previous) => ({ ...previous, ...dateRangeToSearch(next), page: undefined }),
      replace: true,
    })
  }

  const tabs = canViewCandidates ? (
    <SegmentedControl<QoraRoyxatTab>
      aria-label={t('flags.page.blacklist.title')}
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
      <BlacklistCandidatesPage
        search={q ?? ''}
        onSearchChange={changeSearch}
        range={range}
        onRangeChange={changeRange}
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
    <FlagListPage
      type="blacklist"
      search={q ?? ''}
      onSearchChange={changeSearch}
      range={range}
      onRangeChange={changeRange}
      tabs={tabs}
    />
  )
}
