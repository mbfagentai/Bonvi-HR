import { useNavigate } from '@tanstack/react-router'
import { ListFilter, Search, SearchX, X } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import type { EmployeeFlagType } from '@/features/employees/api/employees-api'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import {
  FLAG_LOOK,
  LISTS_MANAGE,
  LISTS_VIEW,
  REPORTS_EXPORT,
} from '@/features/employees/lib/employee-presentation'
import { useListExport } from '@/features/reports/api/list-export'
import { ListExportButton, ListExportError } from '@/features/reports/components/list-export'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  NO_DATE_RANGE,
  hasDateRange,
  isDateRangeInverted,
  useSearchDraft,
  type DateRange,
} from '@/shared/lib/list-search'
import {
  Alert,
  Button,
  Card,
  DataTable,
  DateRangeField,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Switch,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import { flaggedListParams, type FlaggedEmployee } from '../api/flags-api'
import { useFlagged } from '../api/flags-queries'
import { CloseFlagDialog } from './close-flag-dialog'

const helper = dataColumnHelper<FlaggedEmployee>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: FlaggedEmployee[] = []

/**
 * Fayl nomi server bermasa — belgi turidan (SPEC-excel-eksport §2:
 * «slug from the flag type»). Odatda nomni server `Content-Disposition` da beradi.
 */
const EXPORT_SLUG: Readonly<Record<EmployeeFlagType, string>> = {
  reserve: 'rezerv_xodimlar',
  blacklist: 'qora_royxat_xodimlar',
  monitoring: 'nazoratdagilar',
  capable: 'qobiliyatli',
}

/**
 * Belgi ro'yxati — rezerv, qora ro'yxat va nazorat uchun BITTA komponent.
 *
 * Uchala sahifaning ustunlari, amallari va xatti-harakati bir xil; farqi
 * faqat sarlavha, ikonka va tushuntirish matnida. Uchta nusxa yozilsa,
 * keyingi o'zgarish (masalan yangi ustun) ikkitasida qilinib, uchinchisi
 * unutilardi — bu loyihada ma'lumotnomalar ham aynan shu sababdan bitta
 * komponentdan yasalgan.
 *
 * <b>Yangi belgi qo'yish bu yerda ATAYLAB yo'q.</b> Belgi xodimga
 * qo'yiladi, ro'yxatga emas — shu sababli u xodim profilidan boshlanadi.
 * Bu yerda ro'yxatni ko'rish va belgini yopish qoladi.
 *
 * <b>Qidiruv holati PROPS orqali keladi, ichkarida `useState` bilan emas.</b>
 * NEGA: u manzil satrida yashaydi (loyihaning qat'iy konvensiyasi), manzil
 * esa marshrutga tegishli — bu komponent esa to'rtta marshrutga xizmat
 * qiladi. `useSearch({ strict: false })` bilan uni ichkaridan o'qish ham
 * mumkin edi, lekin u holda tip to'rtta marshrutning birlashmasiga
 * aylanardi va noto'g'ri marshrutga qo'shilgan sahifa hech qanday xato
 * bermasdi. Props esa har bir marshrutni o'z holatini ochiq berishga
 * majbur qiladi.
 */
export function FlagListPage({
  type,
  search,
  onSearchChange,
  range,
  onRangeChange,
  tabs,
}: {
  type: EmployeeFlagType
  /** Manzildagi qidiruv matni. Bo'sh satr — qidiruvsiz. */
  search: string
  onSearchChange: (next: string) => void
  /**
   * Belgi QO'YILGAN sana oralig'i — manzildan, qidiruv bilan bir xil
   * sabab bilan props orqali. «Faqat kuchdagilar» bilan birga ishlaydi:
   * ikkalasi «VA» bilan birlashadi (server qoidasi).
   */
  range: DateRange
  onRangeChange: (next: DateRange) => void
  /**
   * Sarlavhadagi bo'lim almashtirgichi. Hozir faqat «Rezerv» da:
   * «Nomzodlar | Xodimlar» (sababi `ReserveCandidatesPage` izohida).
   */
  tabs?: ReactNode
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()

  const canManage = hasPermission(LISTS_MANAGE)
  // Eksport — ro'yxatni ko'rish VA `reports.export` (SPEC-excel-eksport §1)
  const canExport = hasPermission(LISTS_VIEW) && hasPermission(REPORTS_EXPORT)
  const look = FLAG_LOOK[type]

  const [onlyActive, setOnlyActive] = useState(true)
  const [closing, setClosing] = useState<FlaggedEmployee | null>(null)

  /*
   * Qidiruv har harfda so'rov yubormaydi — yozish to'xtagach 350 ms
   * kutiladi (`employments-toolbar.tsx` dagi bilan bir xil hook).
   */
  const [draft, setDraft] = useSearchDraft(search, onSearchChange)

  /*
   * Sana oralig'i yig'ilgan panelda (`IncentivesToolbar` naqshi): qidiruv
   * kundalik, oraliq esa kamdan-kam. Oraliq qo'yilgan bo'lsa panel ochiq
   * boshlanadi — aks holda HR «nega ro'yxat qisqa?» deb so'rardi.
   */
  const rangeCount = [range.from, range.to].filter((value) => value !== '').length
  const [rangeOpen, setRangeOpen] = useState(rangeCount > 0)

  /*
   * Teskari oraliq bilan so'rov YUBORILMAYDI: server baribir 400 qaytaradi,
   * ekran esa sababni qidiruv ostida darhol aytadi
   * (`attendance-passes-page.tsx` naqshi).
   */
  const rangeInvalid = isDateRangeInverted(range)

  const list = useFlagged(type, onlyActive, search, range, !rangeInvalid)
  // Teskari oraliqda eski natija ko'rinib qolmasin — u boshqa filtrniki
  const rows = rangeInvalid ? EMPTY : (list.data ?? EMPTY)

  // Xato mutatsiyada qoladi — keyingi bosish uni o'zi tozalaydi (`employees-page.tsx`)
  const exportList = useListExport()

  const columns = useMemo<Array<DataColumn<FlaggedEmployee>>>(
    () => [
      helper.accessor('employeeName', {
        header: t('employee.fullName'),
        /*
          ⭐ BIR QATOR (egasi 2026-10-22): rasm va to'liq ism bir chiziqda,
          ism kesilmaydi — ustun eng uzun ismgacha kengayadi (`DataTable` —
          `whitespace-nowrap`). Ilgari 300px da uch so'zli ism kesilardi.
        */
        cell: ({ row }) => (
          <div className="flex items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId}
              photoKey={row.original.photoKey}
              name={row.original.employeeName}
              size="md"
            />
            <span className="font-medium">{row.original.employeeName}</span>
          </div>
        ),
      }),

      helper.accessor('reason', {
        header: t('flags.reason'),
        enableSorting: false,
        /*
          «Sabab» — Excel'dan kelgan erkin matn, ba'zan bir necha jumla.
          Bir qatorda, 360px da kesiladi; to'lig'i `title` da (sichqoncha
          ustida). Kesilmasa butun jadval eng uzun sabab eniga cho'zilardi.
        */
        cell: ({ getValue }) => {
          const reason = getValue()

          if (reason === null) {
            return <span className="text-[var(--fg-tertiary)]">—</span>
          }

          return (
            <span className="block max-w-[360px] truncate text-[var(--fg-secondary)]" title={reason}>
              {reason}
            </span>
          )
        },
      }),

      helper.accessor('startDate', {
        header: t('flags.startDate'),
        meta: { width: '130px' },
        cell: ({ getValue }) => <DateText value={getValue()} />,
      }),

      helper.accessor('endDate', {
        header: t('flags.endDate'),
        meta: { width: '130px', hideOnMobile: true },
        cell: ({ getValue }) => <DateText value={getValue()} />,
      }),

      helper.display({
        id: 'actions',
        header: '',
        /*
          Bitta tugma, `variant="secondary" size="sm"`. Hisob (eng uzun
          tarjima — ruscha «Закрыть», 7 belgi):
            matn:      7 × ~7.5px (text-footnote 13px, font-medium) ≈ 53px
            tugma ichki bo'shlig'i: px-3 × 2                        = 24px
            tugma chegarasi (border 1px × 2, secondary variantda)   =  2px
            katak to'ldirishi: px-4 × 2                             = 32px
            ---------------------------------------------------------
            jami kerak                                             ≈ 111px
          130px ≈ 19px zaxira qoldiradi — tugma chekkaga TEGMAYDI, oddiy
          ehtiyot zaxirasi (font o'lchash noaniqligi uchun ham yetarli:
          8px/belgi kabi «yomonroq» taxminda ham 114px chiqadi, hali ham
          130px ichida). `align: 'end'` bo'lgani uchun bu zaxira chapda
          ko'rinmas holda qoladi — 150px shunchaki isrof bo'lardi.
        */
        meta: { width: '130px', align: 'end' },
        cell: ({ row }) => {
          // Yopilgan belgini qayta yopib bo'lmaydi — domen rad etadi.
          if (!canManage || row.original.endDate !== null) return null

          return (
            <span className="flex justify-end">
              <Button
                size="sm"
                variant="secondary"
                onClick={(event) => {
                  // Qator bosilganda profilga o'tadi — tugma buni to'xtatadi.
                  event.stopPropagation()
                  setClosing(row.original)
                }}
              >
                {t('flags.close')}
              </Button>
            </span>
          )
        },
      }),
    ],
    [canManage, t],
  )

  return (
    // Ekranning butun eni — jadval bir qatorli, ustunlari ko'p (`app-shell.tsx`)
    <div data-wide-list className="space-y-5">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={look.icon} tone={look.iconTone} />
            {t(`flags.page.${look.key}.title`)}
          </span>
        }
        subtitle={
          list.isSuccess
            ? t('flags.subtitle', { n: num(rows.length) })
            : t(`flags.page.${look.key}.hint`)
        }
        // Tablar va «faqat kuchdagilar» — sahifada, avvalgi joyida (egasi 2026-10-22)
        toolbar={
          <div className="flex flex-wrap items-center gap-3">
            {tabs}
            <Switch
              checked={onlyActive}
              onChange={setOnlyActive}
              label={t('flags.onlyActive')}
            />
          </div>
        }
      />

      <Card>
        {/*
          Qidiruv jadvaldan YUQORIDA, kartaning ichida: «faqat kuchdagilar»
          kalitidan farqli o'laroq u kundalik ishlatiladi va sahifa
          sarlavhasiga emas, ro'yxatning o'ziga tegishli.
        */}
        <div className="flex flex-wrap items-center gap-2.5 border-b border-[var(--border)] px-4 py-3.5 sm:gap-3 sm:px-5">
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
            variant={rangeOpen ? 'soft' : 'secondary'}
            aria-expanded={rangeOpen}
            onClick={() => setRangeOpen((previous) => !previous)}
          >
            <ListFilter />
            <span className="hidden sm:inline">{t('common.filters')}</span>
            {rangeCount > 0 && (
              <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
                {num(rangeCount)}
              </span>
            )}
          </Button>

          {/*
            Tozalash — «Filtrlar» yonida, faqat filtr qo'yilganda. Panel ostidagi
            alohida qator ro'yxatdan joy olardi (egasi 2026-10-22).
          */}
          {hasDateRange(range) && (
            <Button
              type="button"
              pill
              variant="ghost"
              title={t('employees.clearFilters')}
              aria-label={t('employees.clearFilters')}
              onClick={() => onRangeChange(NO_DATE_RANGE)}
            >
              <X />
              <span className="hidden xl:inline">{t('employees.clearFilters')}</span>
            </Button>
          )}

          {/*
            Eksport — «Filtrlar» yonida. «Faqat kuchdagilar» ham faylga tushadi:
            parametrlar ro'yxat so'rovi bilan bitta funksiyadan.
          */}
          {canExport && (
            <ListExportButton
              onClick={() =>
                exportList.mutate({
                  path: '/api/employees/flagged/export',
                  params: flaggedListParams(type, onlyActive, search, range),
                  slug: EXPORT_SLUG[type],
                })
              }
              pending={exportList.isPending}
              disabled={rangeInvalid}
            />
          )}
        </div>

        {rangeOpen && (
          <div className="border-b border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {/* Oraliq belgi QO'YILGAN kun bo'yicha — yorliq shuni aytadi */}
              <DateRangeField
                label={t('dateRange.marked')}
                fromLabel={t('flags.markedFrom')}
                toLabel={t('flags.markedTo')}
                from={range.from}
                to={range.to}
                onFromChange={(value) => onRangeChange({ ...range, from: value })}
                onToChange={(value) => onRangeChange({ ...range, to: value })}
              />
            </div>
          </div>
        )}

        {rangeInvalid && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="warning">{errorMessage('royxat.sana_oraligi_notogri')}</Alert>
          </div>
        )}

        <ListExportError error={exportList.error} />

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          loading={list.isPending && !rangeInvalid}
          rowLabel={(row) => row.employeeName}
          onRowClick={(row) =>
            void navigate({
              to: '/xodimlar/$employeeId',
              params: { employeeId: row.employeeId },
            })
          }
          empty={
            // Teskari oraliqda bo'sh holat chizilmaydi — sabab ogohlantirishda.
            // Oraliq qo'yilgan bo'lsa — «topilmadi», ro'yxat bo'sh EMAS.
            rangeInvalid ? undefined : hasDateRange(range) ? (
              <EmptyState
                icon={SearchX}
                title={t('common.empty')}
                description={t('common.dateRangeEmptyText')}
              />
            ) : (
              <EmptyState
                icon={look.icon}
                title={t(`flags.page.${look.key}.emptyTitle`)}
                description={t(`flags.page.${look.key}.hint`)}
              />
            )
          }
        />
      </Card>

      <CloseFlagDialog item={closing} onClose={() => setClosing(null)} />
    </div>
  )
}

function DateText({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
