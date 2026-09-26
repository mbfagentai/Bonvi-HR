import { useNavigate } from '@tanstack/react-router'
import { Search } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import type { EmployeeFlagType } from '@/features/employees/api/employees-api'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { FLAG_LOOK, LISTS_MANAGE } from '@/features/employees/lib/employee-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { useSearchDraft } from '@/shared/lib/list-search'
import {
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Switch,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type { FlaggedEmployee } from '../api/flags-api'
import { useFlagged } from '../api/flags-queries'
import { CloseFlagDialog } from './close-flag-dialog'

const helper = dataColumnHelper<FlaggedEmployee>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: FlaggedEmployee[] = []

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
  tabs,
}: {
  type: EmployeeFlagType
  /** Manzildagi qidiruv matni. Bo'sh satr — qidiruvsiz. */
  search: string
  onSearchChange: (next: string) => void
  /**
   * Sarlavhadagi bo'lim almashtirgichi. Hozir faqat «Rezerv» da:
   * «Nomzodlar | Xodimlar» (sababi `ReserveCandidatesPage` izohida).
   */
  tabs?: ReactNode
}) {
  const { t, num } = useLocale()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()

  const canManage = hasPermission(LISTS_MANAGE)
  const look = FLAG_LOOK[type]

  const [onlyActive, setOnlyActive] = useState(true)
  const [closing, setClosing] = useState<FlaggedEmployee | null>(null)

  /*
   * Qidiruv har harfda so'rov yubormaydi — yozish to'xtagach 350 ms
   * kutiladi (`employments-toolbar.tsx` dagi bilan bir xil hook).
   */
  const [draft, setDraft] = useSearchDraft(search, onSearchChange)

  const list = useFlagged(type, onlyActive, search)
  const rows = list.data ?? EMPTY

  const columns = useMemo<Array<DataColumn<FlaggedEmployee>>>(
    () => [
      helper.accessor('employeeName', {
        header: t('employee.fullName'),
        /*
          Pastki chegara ATAYLAB qo'yildi — avval umuman yo'q edi. Hisob:
          300px − katak to'ldirishi (px-4 × 2 = 32px) = 268px; 268px −
          rasm (size-12 = 48px) − oraliq (gap-3 = 12px) = 208px sof matnga
          qoladi. `text-subhead` (15px) da bu ~24-26 belgi — o'zbekcha
          uch so'zli to'liq ism («Абдуқодиров Шерзод Нурмат ўғли», 30
          belgi) baribir kesiladi (title yo'q, to'liq ism qatorni bosib
          profilga o'tishda ko'rinadi), lekin odatiy ikki-uch so'zli ism
          endi sig'adi. 260px yetarli emas edi — sof matnga atigi 168px
          (~20 belgi) qolardi. Xodimlar ro'yxatida xuddi shu konstruksiya
          (rasm+ism) 340px, sinov ro'yxatida 300px — bu yerda ham 300px,
          uch ro'yxatda bir xil odam bir xil kenglikda ko'rinsin.
        */
        meta: { width: '300px' },
        cell: ({ row }) => (
          <div className="flex min-w-0 items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId}
              photoKey={row.original.photoKey}
              name={row.original.employeeName}
              size="lg"
            />
            <p className="truncate-1 font-medium">{row.original.employeeName}</p>
          </div>
        ),
      }),

      helper.accessor('reason', {
        header: t('flags.reason'),
        enableSorting: false,
        /*
          Bu yerda ham pastki chegara yo'q edi. «Sabab» — Excel'dan kelgan
          erkin matn, ba'zan bir necha jumla; kengligi belgilanmasa
          avtomatik jadval uni ENG UZUN qiymatning o'ralmagan kengligiga
          qarab o'lchaydi va butun jadvalni cho'zib yuboradi. 220px — ikki
          qatorda ~40-45 belgi ko'rsatadi (line-clamp-2 pastda), bu odatiy
          sababni tanib olish uchun yetarli; to'lig'i `title` atributida
          turibdi (pastdagi kod, o'zgarmagan).
        */
        meta: { width: '220px' },
        cell: ({ getValue }) => {
          const reason = getValue()

          if (reason === null) {
            return <span className="text-[var(--fg-tertiary)]">—</span>
          }

          // Sabab uzun bo'lishi mumkin (Excel'dan kelgan matnlar bir necha
          // jumla). Ikki qatorda kesiladi, to'lig'i esa `title` da qoladi.
          return (
            <span className="truncate-2 text-[var(--fg-secondary)]" title={reason}>
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
    <div className="space-y-5">
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
        actions={
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
        <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
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

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          loading={list.isPending}
          rowLabel={(row) => row.employeeName}
          onRowClick={(row) =>
            void navigate({
              to: '/xodimlar/$employeeId',
              params: { employeeId: row.employeeId },
            })
          }
          empty={
            <EmptyState
              icon={look.icon}
              title={t(`flags.page.${look.key}.emptyTitle`)}
              description={t(`flags.page.${look.key}.hint`)}
            />
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
