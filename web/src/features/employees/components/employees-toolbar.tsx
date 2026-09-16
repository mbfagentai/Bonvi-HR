import {
  FileSpreadsheet,
  ListFilter,
  LoaderCircle,
  Search,
  TriangleAlert,
  X,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button, Input, SegmentedControl, Select } from '@/shared/ui'
import type { FilterOptions } from '../api/employees-queries'
/*
 * Filtr holatining shakli manzil (URL) bilan bir joyda turadi.
 *
 * NEGA: bu holat endi toolbar'niki emas — u manzil satrida yashaydi va
 * ikkala tomonga o'giriladi. Tip ham, o'girish ham bitta faylda bo'lsa,
 * yangi filtr qo'shilganda ikkalasi birga o'zgaradi.
 */
import {
  DEFAULT_FILTERS,
  DEFAULT_STATUS,
  type EmployeeStatusFilterValue,
  type EmployeesFilterState,
  type EmployeesView,
} from '../lib/employees-search'

/**
 * Qidiruvdan tashqari nechta filtr yoqilgan — tugmadagi sanoq uchun.
 *
 * Holat STANDARTDAN farq qilgandagina sanaladi: ro'yxat baribir
 * ishlayotganlarni ko'rsatadi, ya'ni «employed» — filtr emas, oddiy
 * ko'rinish.
 * Aks holda tugmada doim «1» turib, sanoq ma'nosini yo'qotardi.
 */
function activeFilterCount(filters: EmployeesFilterState): number {
  const ids = [
    filters.departmentId,
    filters.positionId,
    filters.directionId,
    filters.legalEntityId,
  ].filter((value) => value !== '').length

  return filters.status === DEFAULT_STATUS ? ids : ids + 1
}

/**
 * Ro'yxat ustidagi boshqaruv.
 *
 * Birinchi qatorda faqat kundalik ishlatiladiganlar: qidiruv,
 * «tekshirish kerak» va ko'rinish. Qolgan beshta filtr yig'ilgan panelda —
 * ular kamdan-kam o'zgaradi, lekin doim ko'rinib tursa ekranning yarmini
 * egallab, ro'yxatni pastga surib yuboradi.
 */
export function EmployeesToolbar({
  filters,
  onChange,
  view,
  onViewChange,
  options,
  onExport,
  exporting = false,
}: {
  filters: EmployeesFilterState
  onChange: (next: EmployeesFilterState) => void
  view: EmployeesView
  onViewChange: (view: EmployeesView) => void
  options: FilterOptions
  /**
   * «Excelga yuklash». `undefined` — tugma umuman chizilmaydi.
   *
   * NEGA ruxsat va so'rovning o'zi bu yerda emas: toolbar filtrlarni
   * chizadi, u kimning nimaga haqqi borligini bilmaydi (ishga qabul
   * tugmasi ham sahifadan aynan shunday beriladi). Muhimrog'i esa
   * eksport SARALASHNI ham talab qiladi, saralash manzilda yashaydi va
   * toolbar unga umuman tegmaydi — filtr va saralashni ikki komponentga
   * bo'lib yuborsak, «ekranda nima ko'rsang, faylda ham shu» qoidasini
   * ushlab turadigan yagona joy qolmasdi.
   */
  onExport?: () => void
  exporting?: boolean
}) {
  const { t, num } = useLocale()

  const count = activeFilterCount(filters)
  // Filtr yoqilgan bo'lsa panel ochiq boshlanadi: aks holda foydalanuvchi
  // «nega ro'yxat qisqa?» degan savolga javob topolmaydi
  const [open, setOpen] = useState(count > 0)

  /*
   * Qidiruv har harfda serverga so'rov yubormaydi — yozish to'xtagach
   * 350 ms kutiladi. 540 xodimda bu «F.I.Sh. bo'yicha qidirish» ni
   * 15 ta o'rniga 1 ta so'rovga aylantiradi.
   */
  const [draft, setDraft] = useState(filters.search)

  /*
   * Eng so'nggi `filters` va `onChange` havolada saqlanadi.
   *
   * NEGA: ular effekt bog'liqligida bo'lsa, ota komponentning har bir
   * qayta render'i (masalan so'rov javobi kelganda) taymerni noldan
   * boshlab yuboradi va qidiruv hech qachon ishga tushmasligi mumkin.
   * Havola bilan effekt faqat `draft` o'zgarganda qayta o'rnatiladi.
   */
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

  const set = <K extends keyof EmployeesFilterState>(
    key: K,
    value: EmployeesFilterState[K],
  ) => {
    onChange({ ...filters, [key]: value })
  }

  const clearAll = () => {
    onChange(DEFAULT_FILTERS)
    setDraft('')
  }

  /*
    Birinchi qiymat — ro'yxatning STANDART ko'rinishi: sinovdagilar va
    doimiylar birga. U «barcha holatlar» dan farq qiladi: ishdan
    ketganlar bu yerga kirmaydi (ular tarix va alohida bo'limda).
  */
  const statusOptions = [
    { value: 'employed', label: t('employee.statusEmployed') },
    { value: 'active', label: t('employee.statusActive') },
    { value: 'probation', label: t('employee.statusProbation') },
    { value: 'terminated', label: t('employee.statusTerminated') },
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
            placeholder={t('employees.searchPlaceholder')}
            aria-label={t('common.search')}
          />
        </div>

        {/* «Tekshirish kerak» — alohida chiqarilgan, chunki bu HR ning
            kundalik ish ro'yxati: Excel'dan ko'chirilgan yozuvlarni
            birma-bir tekshirib chiqish kerak */}
        <Button
          type="button"
          pill
          variant={filters.needsReview ? 'soft' : 'secondary'}
          aria-pressed={filters.needsReview}
          onClick={() => set('needsReview', !filters.needsReview)}
          className={cn(filters.needsReview && 'text-ios-orange')}
          title={t('employees.needsReviewFilter')}
        >
          <TriangleAlert
            className={filters.needsReview ? 'text-ios-orange' : 'text-[var(--fg-tertiary)]'}
          />
          <span className="hidden lg:inline">{t('employees.needsReviewFilter')}</span>
        </Button>

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
              {num(count)}
            </span>
          )}
        </Button>

        {/* Eksport — «Filtrlar» yonida va bu ataylab: fayl aynan shu
            panelda yig'ilgan filtrga tushadi, ya'ni ikkalasi bitta
            fikrning davomi. Sarlavhadagi «Ishga qabul» yonida turgan
            bo'lsa, u umumiy «hisobot» tugmasidek ko'rinardi. */}
        {onExport !== undefined && (
          <Button
            type="button"
            pill
            onClick={onExport}
            /* Ikki marta bosilsa ikkita fayl yuklanardi va ikkinchisi
               tezlik cheklovini bekorga yeb qo'yardi */
            disabled={exporting}
            title={t('common.export')}
            aria-label={t('common.export')}
          >
            {exporting ? <LoaderCircle className="animate-spin" /> : <FileSpreadsheet />}
            {/* Yorliq faqat keng ekranda: «Excelga yuklash» uzun va tor
                oynada u qidiruv maydonini siqib qo'yardi */}
            <span className="hidden lg:inline">
              {exporting ? t('employees.exporting') : t('common.export')}
            </span>
          </Button>
        )}

        <SegmentedControl
          value={view}
          onChange={onViewChange}
          aria-label={t('employees.viewLabel')}
          options={[
            { value: 'table', label: t('employees.viewTable'), title: t('employees.viewTable') },
            { value: 'cards', label: t('employees.viewCards'), title: t('employees.viewCards') },
          ]}
        />
      </div>

      {open && (
        <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            <Select
              label={t('employee.department')}
              placeholder={t('employees.anyDepartment')}
              options={options.departments}
              value={filters.departmentId}
              onChange={(event) => set('departmentId', event.target.value)}
            />
            <Select
              label={t('employee.position')}
              placeholder={t('employees.anyPosition')}
              options={options.positions}
              value={filters.positionId}
              onChange={(event) => set('positionId', event.target.value)}
            />
            <Select
              label={t('employee.direction')}
              placeholder={t('employees.anyDirection')}
              options={options.directions}
              value={filters.directionId}
              onChange={(event) => set('directionId', event.target.value)}
            />
            <Select
              label={t('employee.legalEntity')}
              placeholder={t('employees.anyLegalEntity')}
              options={options.legalEntities}
              value={filters.legalEntityId}
              onChange={(event) => set('legalEntityId', event.target.value)}
            />
            <Select
              label={t('employee.status')}
              placeholder={t('employees.anyStatus')}
              options={statusOptions}
              value={filters.status}
              onChange={(event) =>
                set('status', event.target.value as EmployeeStatusFilterValue | '')
              }
            />
          </div>

          {(count > 0 || filters.search !== '' || filters.needsReview) && (
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
