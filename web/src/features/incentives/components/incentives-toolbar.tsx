import { FileSpreadsheet, ListFilter, LoaderCircle, Search, X } from 'lucide-react'
import { useState } from 'react'

import type { FilterOptions } from '@/features/employees/api/employees-queries'
import {
  INCENTIVE_KINDS,
  type IncentiveKindValue,
} from '@/features/employees/api/incentives-api'
import { INCENTIVE_KIND_LOOK } from '@/features/employees/lib/employee-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { useSearchDraft } from '@/shared/lib/list-search'
import { Button, DateRangeField, Input, Select } from '@/shared/ui'
/*
 * Filtr holatining shakli manzil bilan bir joyda turadi
 * (`employments-toolbar.tsx` dagi bilan bir xil sabab).
 */
import { DEFAULT_FILTERS, type IncentivesFilterState } from '../lib/incentives-search'

/** Qidiruvdan tashqari nechta filtr yoqilgan — tugmadagi sanoq uchun. */
function activeFilterCount(filters: IncentivesFilterState): number {
  return [
    filters.kind,
    filters.departmentId,
    filters.legalEntityId,
    filters.from,
    filters.to,
  ].filter((value) => value !== '').length
}

/**
 * «Rag'bat va jarima» ro'yxati ustidagi boshqaruv.
 *
 * `EmploymentsToolbar` bilan bir xil naqsh: kundalik ishlatiladigan
 * qidiruv doim ko'rinadi, qolgan beshta filtr esa yig'ilgan panelda —
 * ular kamdan-kam o'zgaradi. Ikki ekran bir xil his qilishi kerak.
 */
export function IncentivesToolbar({
  filters,
  onChange,
  options,
  onExport,
  exporting = false,
}: {
  filters: IncentivesFilterState
  onChange: (next: IncentivesFilterState) => void
  options: FilterOptions
  /**
   * «Excelga yuklash». `undefined` — tugma umuman chizilmaydi (ruxsat yo'q).
   * So'rov sahifada: eksport saralashni ham oladi, u esa manzilda yashaydi
   * (`EmployeesToolbar` dagi izoh).
   */
  onExport?: () => void
  exporting?: boolean
}) {
  const { t } = useLocale()

  const count = activeFilterCount(filters)
  const [open, setOpen] = useState(count > 0)

  /*
   * Qidiruv har harfda so'rov yubormaydi — yozish to'xtagach 350 ms
   * kutiladi (`employments-toolbar.tsx` dagi bilan bir xil sabab va
   * endi bir xil hook).
   */
  const [draft, setDraft] = useSearchDraft(filters.search, (value) => {
    onChange({ ...filters, search: value })
  })

  const set = <K extends keyof IncentivesFilterState>(
    key: K,
    value: IncentivesFilterState[K],
  ) => {
    onChange({ ...filters, [key]: value })
  }

  const clearAll = () => {
    onChange(DEFAULT_FILTERS)
    setDraft('')
  }

  /*
   * «Hammasi» — bo'sh qiymat, ya'ni «rag'bat ham, jarima ham». Yangi
   * kalit yaratilmadi: `common.all` allaqachon to'rtta tilda bor va
   * aynan shu ma'noni beradi.
   *
   * Yorliqlar `INCENTIVE_KIND_LOOK` dan olinadi: xodim kartasidagi tab
   * ham, dialog ham aynan shu jadvaldan o'qiydi va uchala joyda
   * «Rag'bat» / «Jarima» so'zlari bir xil manbadan chiqadi.
   */
  const kindOptions = [
    { value: '', label: t('common.all') },
    ...INCENTIVE_KINDS.map((kind) => ({
      value: kind,
      label: t(INCENTIVE_KIND_LOOK[kind].labelKey),
    })),
  ]

  return (
    <div className="border-b border-[var(--border)]">
      <div className="flex flex-wrap items-center gap-2.5 px-4 py-3.5 sm:gap-3 sm:px-5">
        {/*
          Placeholder endi UMUMIY kalitda. Eski
          `incentives.searchPlaceholder` «Xodim F.I.Sh. bo'yicha qidirish»
          deydi, server esa endi JSHSHIR, pasport va telefonni ham
          qidiradi. Aynan shu farq xatoning o'zi edi: xodimlar ro'yxatida
          JSHSHIR bilan odamni topgan HR shu matnni bu yerga ko'chirganda
          bo'sh natija olardi.
        */}
        <div className="min-w-[200px] flex-1">
          <Input
            icon={Search}
            shape="pill"
            type="search"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={t('common.searchPlaceholder')}
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

        {/* Eksport — «Filtrlar» yonida: fayl aynan shu filtrlarga tushadi
            (xodimlar ro'yxatidagi bilan bir xil joy). */}
        {onExport !== undefined && (
          <Button
            type="button"
            pill
            onClick={onExport}
            // Ikki marta bosilsa ikkita fayl va tezlik cheklovi bekorga yeyiladi
            disabled={exporting}
            title={t('common.export')}
            aria-label={t('common.export')}
          >
            {exporting ? <LoaderCircle className="animate-spin" /> : <FileSpreadsheet />}
            <span className="hidden lg:inline">
              {exporting ? t('employees.exporting') : t('common.export')}
            </span>
          </Button>
        )}
      </div>

      {open && (
        <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            <Select
              size="sm"
              label={t('incentives.kind')}
              options={kindOptions}
              value={filters.kind}
              onChange={(event) =>
                set('kind', event.target.value as IncentiveKindValue | '')
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
              fromLabel={t('common.dateFrom')}
              toLabel={t('common.dateTo')}
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
