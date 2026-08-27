import { ListFilter, Search, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

import type { FilterOptions } from '@/features/employees/api/employees-queries'
import {
  INCENTIVE_KINDS,
  type IncentiveKindValue,
} from '@/features/employees/api/incentives-api'
import { INCENTIVE_KIND_LOOK } from '@/features/employees/lib/employee-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { Button, Input, Select, TextField } from '@/shared/ui'
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
}: {
  filters: IncentivesFilterState
  onChange: (next: IncentivesFilterState) => void
  options: FilterOptions
}) {
  const { t } = useLocale()

  const count = activeFilterCount(filters)
  const [open, setOpen] = useState(count > 0)

  /*
   * Qidiruv har harfda so'rov yubormaydi — yozish to'xtagach 350 ms
   * kutiladi (`employments-toolbar.tsx` dagi bilan bir xil sabab).
   */
  const [draft, setDraft] = useState(filters.search)
  const latest = useRef({ filters, onChange })
  latest.current = { filters, onChange }

  useEffect(() => {
    setDraft(filters.search)
  }, [filters.search])

  useEffect(() => {
    if (draft === latest.current.filters.search) return

    const timer = window.setTimeout(() => {
      latest.current.onChange({ ...latest.current.filters, search: draft })
    }, 350)

    return () => window.clearTimeout(timer)
  }, [draft])

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
        <div className="min-w-[200px] flex-1">
          <Input
            icon={Search}
            shape="pill"
            type="search"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={t('incentives.searchPlaceholder')}
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
      </div>

      {open && (
        <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            <Select
              label={t('incentives.kind')}
              options={kindOptions}
              value={filters.kind}
              onChange={(event) =>
                set('kind', event.target.value as IncentiveKindValue | '')
              }
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
            <TextField
              label={t('common.dateFrom')}
              type="date"
              value={filters.from}
              onChange={(event) => set('from', event.target.value)}
            />
            <TextField
              label={t('common.dateTo')}
              type="date"
              value={filters.to}
              onChange={(event) => set('to', event.target.value)}
            />
          </div>

          {(count > 0 || filters.search !== '') && (
            <div className="mt-3.5 flex justify-end">
              <Button type="button" size="sm" variant="ghost" onClick={clearAll}>
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
