import { ListFilter, X } from 'lucide-react'
import { useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Button, Select } from '@/shared/ui'
import { VACANCY_STATUSES, type VacancyStatus } from '../api/recruiting-api'
import { useVacancyTitleOptions } from '../api/recruiting-queries'
/*
 * Filtr holatining shakli manzil bilan BIR JOYDA turadi
 * (`employments-toolbar.tsx` dagi bilan bir xil sabab: qoida ikki
 * faylga bo'linsa, biri o'zgarganda ikkinchisi jimgina eskiradi).
 */
import { DEFAULT_FILTERS, type VacanciesFilterState } from '../lib/vacancies-search'

function activeFilterCount(filters: VacanciesFilterState): number {
  return [filters.status, filters.vacancyTitleId].filter((value) => value !== '').length
}

/**
 * Vakansiyalar ro'yxati ustidagi boshqaruv.
 *
 * ⚠️ Bu yerda QIDIRUV MAYDONI YO'Q — `EmploymentsToolbar` dan asosiy
 * farqi shu. Sabab: backend vakansiyalar uchun `search` parametrini
 * umuman qabul qilmaydi (`ListVacanciesQuery` — faqat holat va e'lon
 * nomi). Va bu to'g'ri qaror: vakansiyaning erkin nomi yo'q, u
 * ma'lumotnomadagi NOM bilan taniladi — ya'ni «qidiruv» bu yerda
 * aslida nom filtri bo'lardi, u esa allaqachon bor. Ishlamaydigan
 * maydon qo'yish esa foydalanuvchiga yolg'on va'da beradi.
 *
 * ⚠️ Lavozim, bo'lim va firma filtrlari OLIB TASHLANDI: vakansiyada
 * bunday maydonlar endi umuman yo'q (backend'dagi `Vacancy` domen
 * izohi). Ular «kim ishga olindi» degan savolga tegishli va unga
 * xodimlar ro'yxati javob beradi.
 *
 * Shu sababli filtr paneli ham YIG'ILMAGAN holda ochiq turadi: yashirib
 * qo'yiladigan qidiruv yo'q, ekranning ustki qatori esa bo'sh qolmasin.
 * Aniqrog'i — panel filtr qo'yilgan bo'lsa ochiq, aks holda yig'ilgan
 * (`EmploymentsToolbar` dagi qoida saqlanadi: kamdan-kam
 * o'zgaradigan filtrlar ko'zni band qilmasin).
 */
export function VacanciesToolbar({
  filters,
  onChange,
}: {
  filters: VacanciesFilterState
  onChange: (next: VacanciesFilterState) => void
}) {
  const { t } = useLocale()

  /*
    FILTRDA — hamma nomlar, faolsizlantirilganlari ham: eski vakansiya
    ro'yxatda turibdi va uni topa olish kerak. Formada esa faqat faollar
    (`vacancy-form-dialog.tsx`).
  */
  const titles = useVacancyTitleOptions(false)

  const count = activeFilterCount(filters)
  const [open, setOpen] = useState(count > 0)

  const set = <K extends keyof VacanciesFilterState>(
    key: K,
    value: VacanciesFilterState[K],
  ) => {
    onChange({ ...filters, [key]: value })
  }

  const statusOptions = [
    { value: '', label: t('recruiting.anyStatus') },
    ...VACANCY_STATUSES.map((status) => ({
      value: status,
      label: t(`recruiting.vacancyStatus.${status.toLowerCase()}`),
    })),
  ]

  return (
    <div className="border-b border-[var(--border)]">
      <div className="flex flex-wrap items-center gap-2.5 px-4 py-3.5 sm:gap-3 sm:px-5">
        {/*
          Holat — YAGONA doim ko'rinadigan filtr: HR kuniga o'nlab marta
          «ochiqlarini ko'rsat» deydi, e'lon nomi bo'yicha esa kamdan-kam
          filtrlaydi.
        */}
        <div className="min-w-[180px] flex-1 sm:max-w-[260px] sm:flex-none">
          <Select
            options={statusOptions}
            value={filters.status}
            aria-label={t('recruiting.status')}
            onChange={(event) => set('status', event.target.value as VacancyStatus | '')}
          />
        </div>

        <Button
          type="button"
          pill
          variant={open ? 'soft' : 'secondary'}
          aria-expanded={open}
          onClick={() => setOpen((previous) => !previous)}
          className="ml-auto"
        >
          <ListFilter />
          <span className="hidden sm:inline">{t('common.filters')}</span>
          {count > 0 && (
            <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
              {count}
            </span>
          )}
        </Button>
      </div>

      {open && (
        <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
          {/*
            Yagona qolgan filtr — bir ustunda: ikkita bo'sh katak
            yonida osilib turishi «bu yerda yana nimadir bo'lishi kerak
            edi» degan taassurot qoldirardi.
          */}
          <div className="grid gap-3 sm:max-w-[420px]">
            <Select
              label={t('recruiting.vacancyTitle')}
              placeholder={t('recruiting.anyVacancyTitle')}
              options={titles.options}
              value={filters.vacancyTitleId}
              disabled={titles.isPending}
              onChange={(event) => set('vacancyTitleId', event.target.value)}
            />
          </div>

          {count > 0 && (
            <div className="mt-3.5 flex justify-end">
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => onChange(DEFAULT_FILTERS)}
              >
                <X />
                {t('employees.clearFilters')}
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
