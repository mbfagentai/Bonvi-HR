import { ListFilter, Search, X } from 'lucide-react'
import { useState } from 'react'

import {
  EMPLOYMENT_EVENT_TYPES,
  type EmploymentEventType,
} from '@/features/employees/api/employees-api'
import type { FilterOptions } from '@/features/employees/api/employees-queries'
import { ListExportButton } from '@/features/reports/components/list-export'
import { useLocale } from '@/shared/i18n/use-locale'
import { useSearchDraft } from '@/shared/lib/list-search'
import { Button, DateRangeField, Input, Select } from '@/shared/ui'
/*
 * Filtr holatining shakli manzil bilan bir joyda turadi
 * (`employees-toolbar.tsx` dagi bilan bir xil sabab).
 */
import { DEFAULT_FILTERS, type EmploymentEventsFilterState } from '../lib/employments-search'

/** Sana oralig'idan tashqari nechta filtr yoqilgan — tugmadagi sanoq uchun. */
function activeFilterCount(filters: EmploymentEventsFilterState): number {
  return [
    filters.type,
    filters.departmentId,
    filters.legalEntityId,
    filters.from,
    filters.to,
  ].filter((value) => value !== '').length
}

/**
 * Buyruqlar ro'yxati ustidagi boshqaruv.
 *
 * `EmployeesToolbar` bilan bir xil naqsh: kundalik ishlatiladigan qidiruv
 * doim ko'rinadi, qolgan beshta filtr esa yig'ilgan panelda — ular
 * kamdan-kam o'zgaradi.
 */
export function EmploymentsToolbar({
  filters,
  onChange,
  options,
  onExport,
  exporting = false,
  exportDisabled = false,
}: {
  filters: EmploymentEventsFilterState
  onChange: (next: EmploymentEventsFilterState) => void
  options: FilterOptions
  /**
   * «Excelga yuklash». `undefined` — tugma umuman chizilmaydi (ruxsat yo'q).
   * So'rov sahifada: fayl ro'yxat bilan BITTA filtr obyektidan quriladi
   * (`EmployeesToolbar` izohi).
   */
  onExport?: () => void
  exporting?: boolean
  /** Teskari sana oralig'ida — server 400 qaytarardi. */
  exportDisabled?: boolean
}) {
  const { t } = useLocale()

  const count = activeFilterCount(filters)
  const [open, setOpen] = useState(count > 0)

  /*
   * Qidiruv har harfda so'rov yubormaydi — yozish to'xtagach 350 ms
   * kutiladi. Mantiq `useSearchDraft` ga chiqarilgan: u endi sakkizta
   * boshqa ro'yxatda ham ishlatiladi va nusxalar bir-biridan ajralib
   * ketmasligi kerak.
   */
  const [draft, setDraft] = useSearchDraft(filters.search, (value) => {
    onChange({ ...filters, search: value })
  })

  const set = <K extends keyof EmploymentEventsFilterState>(
    key: K,
    value: EmploymentEventsFilterState[K],
  ) => {
    onChange({ ...filters, [key]: value })
  }

  const clearAll = () => {
    onChange(DEFAULT_FILTERS)
    setDraft('')
  }

  const typeOptions = [
    { value: '', label: t('employments.anyType') },
    ...EMPLOYMENT_EVENT_TYPES.map((type) => ({
      value: type,
      label: t(`employees.events.${type}`),
    })),
  ]

  return (
    <div className="border-b border-[var(--border)]">
      <div className="flex flex-wrap items-center gap-2.5 px-4 py-3.5 sm:gap-3 sm:px-5">
        {/*
          Placeholder ATAYLAB yangi kalitda: eski
          `employments.searchPlaceholder` «Buyruq raqami bo'yicha qidirish»
          deydi va bu endi YARIM haqiqat — server buyruq raqamini ham,
          xodimni ham (F.I.Sh., JSHSHIR, pasport, telefon) qidiradi.
          Noto'g'ri va'da bergan maydonga HR ism yozib ko'rmasdi.
        */}
        <div className="min-w-[200px] flex-1">
          <Input
            icon={Search}
            shape="pill"
            type="search"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={t('employments.searchAnyPlaceholder')}
            aria-label={t('common.search')}
          />
        </div>

        <Button
          type="button"
          pill
          variant={open ? 'soft' : 'secondary'}
          aria-expanded={open}
          onClick={() => setOpen((previous) => !previous)}
        >
          <ListFilter />
          <span className="hidden sm:inline">{t('common.filters')}</span>
          {count > 0 && (
            <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
              {count}
            </span>
          )}
        </Button>

        {/*
          Tozalash — «Filtrlar» yonida, faqat filtr qo'yilganda. Panel ostidagi
          alohida qator ro'yxatdan joy olardi (egasi 2026-10-22).
        */}
        {(count > 0 || filters.search !== '') && (
          <Button
            type="button"
            pill
            variant="ghost"
            title={t('employees.clearFilters')}
            aria-label={t('employees.clearFilters')}
            onClick={clearAll}
          >
            <X />
            <span className="hidden xl:inline">{t('employees.clearFilters')}</span>
          </Button>
        )}

        {/* Eksport — «Filtrlar» yonida: fayl aynan shu filtrga tushadi (xodimlar ro'yxatidagi joy) */}
        {onExport !== undefined && (
          <ListExportButton onClick={onExport} pending={exporting} disabled={exportDisabled} />
        )}
      </div>

      {open && (
        <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            <Select
              size="sm"
              label={t('employments.type')}
              options={typeOptions}
              value={filters.type}
              onChange={(event) =>
                set('type', event.target.value as EmploymentEventType | '')
              }
            />
            <Select
              size="sm"
              label={t('employee.department')}
              placeholder={t('employees.anyDepartment')}
              options={options.departments}
              value={filters.departmentId}
              onChange={(event) => set('departmentId', event.target.value)}
            />
            <Select
              size="sm"
              label={t('employee.legalEntity')}
              placeholder={t('employees.anyLegalEntity')}
              options={options.legalEntities}
              value={filters.legalEntityId}
              onChange={(event) => set('legalEntityId', event.target.value)}
            />
            <DateRangeField
              label={t('dateRange.date')}
              fromLabel={t('employments.dateFrom')}
              toLabel={t('employments.dateTo')}
              from={filters.from}
              to={filters.to}
              onFromChange={(value) => set('from', value)}
              onToChange={(value) => set('to', value)}
            />
          </div>
        </div>
      )}
    </div>
  )
}
