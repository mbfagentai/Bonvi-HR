import { ListFilter, X } from 'lucide-react'
import { useState } from 'react'

import type { FilterOptions } from '@/features/employees/api/employees-queries'
import { useLocale } from '@/shared/i18n/use-locale'
import { Button, Select } from '@/shared/ui'
import { VACANCY_STATUSES, type VacancyStatus } from '../api/recruiting-api'
/*
 * Filtr holatining shakli manzil bilan BIR JOYDA turadi
 * (`employments-toolbar.tsx` dagi bilan bir xil sabab: qoida ikki
 * faylga bo'linsa, biri o'zgarganda ikkinchisi jimgina eskiradi).
 */
import { DEFAULT_FILTERS, type VacanciesFilterState } from '../lib/vacancies-search'

function activeFilterCount(filters: VacanciesFilterState): number {
  return [
    filters.status,
    filters.positionId,
    filters.departmentId,
    filters.legalEntityId,
  ].filter((value) => value !== '').length
}

/**
 * Vakansiyalar ro'yxati ustidagi boshqaruv.
 *
 * ⚠️ Bu yerda QIDIRUV MAYDONI YO'Q — `EmploymentsToolbar` dan asosiy
 * farqi shu. Sabab: backend vakansiyalar uchun `search` parametrini
 * umuman qabul qilmaydi (`ListVacanciesQuery` — faqat holat, lavozim,
 * bo'lim va firma). Va bu to'g'ri qaror: vakansiyaning erkin nomi yo'q,
 * u LAVOZIM bilan taniladi — ya'ni «qidiruv» bu yerda aslida lavozim
 * filtri bo'lardi, u esa allaqachon bor. Ishlamaydigan maydon qo'yish
 * esa foydalanuvchiga yolg'on va'da beradi.
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
  options,
}: {
  filters: VacanciesFilterState
  onChange: (next: VacanciesFilterState) => void
  options: FilterOptions
}) {
  const { t } = useLocale()

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
          «ochiqlarini ko'rsat» deydi, lavozim yoki firma bo'yicha esa
          kamdan-kam filtrlaydi.
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
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            <Select
              label={t('employee.position')}
              placeholder={t('recruiting.anyPosition')}
              options={options.positions}
              value={filters.positionId}
              onChange={(event) => set('positionId', event.target.value)}
            />
            <Select
              label={t('employee.department')}
              placeholder={t('employees.anyDepartment')}
              options={options.departments}
              value={filters.departmentId}
              onChange={(event) => set('departmentId', event.target.value)}
            />
            <Select
              label={t('employee.legalEntity')}
              placeholder={t('employees.anyLegalEntity')}
              options={options.legalEntities}
              value={filters.legalEntityId}
              onChange={(event) => set('legalEntityId', event.target.value)}
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
