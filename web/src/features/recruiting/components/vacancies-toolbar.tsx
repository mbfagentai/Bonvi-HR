import { Search, X } from 'lucide-react'

import { ListExportButton } from '@/features/reports/components/list-export'
import { useLocale } from '@/shared/i18n/use-locale'
import { useSearchDraft } from '@/shared/lib/list-search'
import { Button, Input, Select } from '@/shared/ui'
import { VACANCY_STATUSES, type VacancyStatus } from '../api/recruiting-api'
/*
 * Filtr holatining shakli manzil bilan BIR JOYDA turadi
 * (`employments-toolbar.tsx` dagi bilan bir xil sabab: qoida ikki
 * faylga bo'linsa, biri o'zgarganda ikkinchisi jimgina eskiradi).
 */
import { DEFAULT_FILTERS, type VacanciesFilterState } from '../lib/vacancies-search'

/**
 * Vakansiyalar ro'yxati ustidagi boshqaruv — BITTA qator: nom bo'yicha
 * qidiruv va holat.
 *
 * <b>Qidiruv endi BOR.</b> Ilgari bu yerda nom FILTRI turardi
 * («Vakansiya nomlari» ma'lumotnomasidan tanlanadigan ro'yxat), chunki
 * nom ma'lumotnomadan olinardi. Ma'lumotnoma olib tashlandi — nom
 * vakansiyaning o'z matni — va uni topishning tabiiy yo'li yozib
 * qidirish. Server nomning istalgan qismini katta-kichik harf farqsiz
 * qidiradi.
 *
 * <b>Yig'iladigan filtr paneli YO'Q</b> (`CandidatesToolbar` dan farqi):
 * qidiruvdan tashqari yagona filtr — holat, va u HR ning kundalik
 * tanlovi («ochiqlarini ko'rsat»). Bitta maydon uchun alohida panel
 * ochish bir bosishni ikkiga aylantirardi. Naqsh — sinov ro'yxatidagi
 * qidiruv + holat qatori (`probations-page.tsx`).
 *
 * ⚠️ Lavozim, bo'lim va firma filtrlari yo'q: vakansiyada bunday
 * maydonlar umuman yo'q (backend'dagi `Vacancy` domen izohi). Ular
 * «kim ishga olindi» degan savolga tegishli va unga xodimlar ro'yxati
 * javob beradi.
 */
export function VacanciesToolbar({
  filters,
  onChange,
  onExport,
  exporting = false,
}: {
  filters: VacanciesFilterState
  onChange: (next: VacanciesFilterState) => void
  /**
   * «Excelga yuklash». `undefined` — tugma umuman chizilmaydi (ruxsat yo'q).
   * So'rov sahifada: fayl ro'yxat bilan bitta filtr va saralashdan quriladi.
   */
  onExport?: () => void
  exporting?: boolean
}) {
  const { t } = useLocale()

  /*
   * Qidiruv har harfda so'rov yubormaydi — yozish to'xtagach 350 ms
   * kutiladi (`useSearchDraft`, boshqa ro'yxatlar bilan bir xil).
   */
  const [draft, setDraft] = useSearchDraft(filters.search, (value) => {
    onChange({ ...filters, search: value })
  })

  const filtered = filters.status !== '' || filters.search !== ''

  const clearAll = () => {
    onChange(DEFAULT_FILTERS)
    setDraft('')
  }

  const statusOptions = [
    { value: '', label: t('recruiting.anyStatus') },
    ...VACANCY_STATUSES.map((status) => ({
      value: status,
      label: t(`recruiting.vacancyStatus.${status.toLowerCase()}`),
    })),
  ]

  return (
    <div className="flex flex-wrap items-center gap-2.5 border-b border-[var(--border)] px-4 py-3.5 sm:gap-3 sm:px-5">
      <div className="min-w-[200px] flex-1">
        <Input
          icon={Search}
          shape="pill"
          type="search"
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          placeholder={t('recruiting.vacancySearchPlaceholder')}
          aria-label={t('common.search')}
        />
      </div>

      <Select
        size="sm"
        options={statusOptions}
        value={filters.status}
        aria-label={t('recruiting.status')}
        onChange={(event) =>
          onChange({ ...filters, status: event.target.value as VacancyStatus | '' })
        }
        className="w-full sm:w-[220px]"
      />

      {/*
        Tozalash — faqat biror narsa tanlanganda. Doim turadigan tugma
        «bu yerda nimadir yoqilgan» degan yolg'on belgi bo'lardi.
      */}
      {filtered && (
        <Button type="button" size="sm" variant="ghost" onClick={clearAll}>
          <X />
          {t('employees.clearFilters')}
        </Button>
      )}

      {/* Eksport — qatorning oxirida: «Filtrlar» tugmasi bu ro'yxatda yo'q */}
      {onExport !== undefined && <ListExportButton onClick={onExport} pending={exporting} />}
    </div>
  )
}
