import { getRouteApi } from '@tanstack/react-router'
import {
  CalendarClock,
  CircleAlert,
  ListFilter,
  Search,
  SearchX,
  Timer,
  UserPlus,
  X,
} from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { HireDialog } from '@/features/employees/components/hire-dialog'
import {
  EMPLOYEES_CREATE,
  EMPLOYMENT_HIRE,
  EMPLOYMENT_PROBATION_COMPLETE,
  EMPLOYMENT_PROBATION_EXTEND,
} from '@/features/employees/lib/employee-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  NO_DATE_RANGE,
  dateRangeToSearch,
  hasDateRange,
  isDateRangeInverted,
  searchToDateRange,
  toSearchParam,
  useSearchDraft,
  type DateRange,
} from '@/shared/lib/list-search'
import { useClampPage, usePageSize } from '@/shared/lib/use-page-size'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  DateField,
  EmptyState,
  Pagination,
  IconTile,
  Input,
  PageHeader,
  Select,
  dataColumnHelper,
  type BadgeTone,
  type DataColumn,
} from '@/shared/ui'

import {
  PROBATION_STATUSES,
  type ProbationFilters,
  type ProbationItem,
  type ProbationStatus,
} from '../api/probations-api'
import { WARNING_DAYS, useEndingSoon, useProbations } from '../api/probations-queries'
import { MissingProbations } from './missing-probations'
import { ExtendDialog, ResolveDialog } from './probation-dialogs'

const helper = dataColumnHelper<ProbationItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: ProbationItem[] = []

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/sinov')

const STATUS_TONE: Readonly<Record<ProbationStatus, BadgeTone>> = {
  inProgress: 'info',
  passed: 'success',
  failed: 'danger',
  leftEarly: 'warning',
  cancelled: 'neutral',
}

/**
 * Sinov muddatidagi xodimlar.
 *
 * HR bu ekrandan bitta narsani so'ragan: <b>muddat tugashiga 7 kun qolganda
 * ogohlantirish</b>. Excel'da bu qo'lda kuzatilardi va shu sababli tez-tez
 * o'tkazib yuborilardi.
 *
 * Ogohlantirish ATAYLAB fon vazifasi (cron) orqali emas, sahifaning
 * o'zidan chiqadi: tizimdan atigi bir necha kishi foydalanadi va ular uni
 * har kuni ochadi. Jonli so'rov doim aniq — kechagi vazifa natijasi emas —
 * va «vazifa ishlamay qolgan, hech kim sezmagan» degan xato holati umuman
 * paydo bo'lmaydi.
 */
export function ProbationsPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  /*
   * «Uzaytirish» va «Yakunlash» — ikki ALOHIDA ruxsat: rol muddatni
   * uzaytira olib, sinovni yopa olmasligi mumkin (va aksincha). Har bir
   * tugma faqat o'z ruxsati bilan chiziladi.
   */
  const canExtend = hasPermission(EMPLOYMENT_PROBATION_EXTEND)
  const canComplete = hasPermission(EMPLOYMENT_PROBATION_COMPLETE)

  /*
   * Ishga qabul ikki ruxsatni birga talab qiladi: shaxs kartasi
   * yaratiladi VA mehnat munosabati ochiladi. Shart `/xodimlar` dagi bilan
   * AYNAN bir xil qoldi — tugma ko'chdi, huquq emas.
   */
  const canHire = hasPermission(EMPLOYEES_CREATE) && hasPermission(EMPLOYMENT_HIRE)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  /**
   * ⭐ Qatorni bosish — XODIM KARTASI (foydalanuvchi talabi, 2026-09-16).
   *
   * Ilgari qator umuman bosilmasdi va sinovdagi odamning kartasiga
   * kirishning yo'li yo'q edi: HR uni «Xodimlar» bo'limida ham topa
   * olmasdi (u yerda sukut filtr faqat doimiylarni ko'rsatardi).
   * Ya'ni 600 dan ortiq odam tizimda bor edi, lekin ularning kartasi
   * hech qayerdan ochilmasdi.
   *
   * Manzil `employeeId` bo'yicha — jadvaldagi `id` sinov YOZUVIniki,
   * xodimniki emas.
   */
  const openEmployee = (row: ProbationItem) => {
    void navigate({ to: '/xodimlar/$employeeId', params: { employeeId: row.employeeId } })
  }

  // Holat sahifaning o'zida, qidiruv esa manzilda — sabab marshrut
  // faylida (`sinov.tsx`) yozilgan.
  //
  // Sukut — «Sinovda» (2026-10-14, egasining talabi). Ilgari «Barcha
  // holatlar» edi va sinovi yakunlangan xodim — muddatidan oldin «o'tdi»
  // yoki «o'tmadi» bo'lsa ham — shu menyuda turib qolardi: HR uni
  // «ko'chmadi» deb ko'rardi. Menyu xodimning HOZIRGI holatini
  // ko'rsatadi; yakunlanganlar tarixi filtrda qoladi.
  const [status, setStatus] = useState<ProbationFilters['status']>('inProgress')
  const [page, setPage] = useState(1)
  const { pageSize, setPageSize, allowAll } = usePageSize('probations', true)
  const [resolving, setResolving] = useState<ProbationItem | null>(null)
  const [extending, setExtending] = useState<ProbationItem | null>(null)
  const [hireOpen, setHireOpen] = useState(false)

  /*
   * Sinov TUGASH sanasi oralig'i — manzilda (`sinov.tsx`). Yig'ilgan panel
   * oraliq qo'yilgan bo'lsa ochiq boshlanadi: aks holda HR «nega ro'yxat
   * qisqa?» degan savolga javob topolmasdi (`IncentivesToolbar` naqshi).
   */
  const range = searchToDateRange(search)
  const rangeCount = [range.from, range.to].filter((value) => value !== '').length
  const [rangeOpen, setRangeOpen] = useState(rangeCount > 0)

  /*
   * Teskari oraliq bilan so'rov YUBORILMAYDI: server baribir 400 qaytaradi,
   * ekran esa sababni toolbar ostida darhol aytadi
   * (`attendance-passes-page.tsx` naqshi).
   */
  const rangeInvalid = isDateRangeInverted(range)

  const filters: ProbationFilters = {
    status,
    search: search.q ?? '',
    from: range.from,
    to: range.to,
    page,
    pageSize,
  }

  /** Oraliq o'zgarsa birinchi sahifadan — 3-sahifa yangi natijada bo'sh bo'lishi mumkin. */
  const changeRange = (next: DateRange) => {
    setPage(1)
    void navigate({
      search: (previous) => ({ ...previous, ...dateRangeToSearch(next) }),
      replace: true,
    })
  }

  /*
   * Qidiruv har harfda so'rov yubormaydi (350 ms) va matn manzilga
   * yoziladi. `replace` — qidiruvni aniqlashtirish alohida tarix qadami
   * emas: aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
   */
  /*
   * Qidiruv yoki holat o'zgarsa BIRINCHI sahifaga qaytiladi.
   *
   * Usiz HR uchinchi sahifada turib qidirsa, natija bir sahifaga
   * sig'sa ham bo'sh ekran ko'rardi — «topilmadi» deb o'ylardi.
   */
  const [draft, setDraft] = useSearchDraft(filters.search, (value) => {
    setPage(1)
    // Avvalgi manzil saqlanadi — qidiruv sana oralig'ini o'chirib yubormasin
    void navigate({
      search: (previous) => ({ ...previous, q: toSearchParam(value) }),
      replace: true,
    })
  })

  const list = useProbations(filters, !rangeInvalid)
  const warning = useEndingSoon()

  /*
   * Ma'lumotnomalar — ishga qabul formasi uchun (firma, bo'lim, lavozim).
   *
   * NEGA oyna ochilishini kutmaydi: ular baribir keshlanadi va boshqa
   * ekranlarda ham so'raladi, oyna esa darhol to'ldirilgan tanlovlar bilan
   * ochilishi kerak — bo'sh ro'yxatli «Bo'limni tanlang» HR ni ikkilantirardi.
   */
  const options = useFilterOptions()

  const rows = rangeInvalid ? EMPTY : (list.data?.items ?? EMPTY)
  const total = rangeInvalid ? 0 : (list.data?.total ?? 0)

  // Eski sahifa raqami yangi hajmda bo'sh bo'lsa — oxirgi sahifaga (`useClampPage`)
  useClampPage(page, pageSize, list.isPlaceholderData ? undefined : list.data?.total, setPage)
  const soonCount = warning.data?.length ?? 0

  const statusOptions = useMemo(
    () => [
      { value: '', label: t('probation.allStatuses') },
      ...Object.keys(PROBATION_STATUSES).map((status) => ({
        value: status,
        label: t(`probation.status.${status}`),
      })),
    ],
    [t],
  )

  const columns = useMemo<Array<DataColumn<ProbationItem>>>(
    () => [
      helper.accessor('employeeName', {
        header: t('employee.fullName'),
        /*
          Aniq kenglik yo'q edi — ustun brauzer xohlagancha torayardi.
          Hisob-kitob: 300px − katak to'ldirishi (`px-4` = 32px) = 268px;
          rasm (`size="lg"` = 48px) + `gap-3` (12px) = 60px; qolgan
          ~208px matnga — bitta qatorli ism (~26 belgi) va ostidagi
          lavozim qatori (~32 belgi, kichikroq shriftda) shu joyga
          sig'adi. Uzunroq ism/lavozim uchida uchta nuqta bilan kesiladi
          (`truncate-1`), bu boshqa matn ustunlaridagi kabi qabul
          qilingan yechim.
        */
        meta: { width: '300px' },
        cell: ({ row }) => {
          const item = row.original

          return (
            <div className="flex min-w-0 items-center gap-3">
              {/*
                Rasm shu yerda ham ko'rsatiladi: HR xodimni ismidan emas,
                yuzidan taniydi va ro'yxatdan ro'yxatga o'tganda bir xil
                ko'rinish saqlanishi kerak.
              */}
              <EmployeePhoto
                employeeId={item.employeeId}
                photoKey={item.photoKey}
                name={item.employeeName}
                size="lg"
              />

              <div className="min-w-0">
                <p className="truncate-1 font-medium">{item.employeeName}</p>
                {item.position !== null && (
                  <p className="truncate-1 mt-0.5 text-caption1 text-[var(--fg-tertiary)]">
                    {item.position}
                  </p>
                )}
              </div>
            </div>
          )
        },
      }),

      helper.accessor('department', {
        header: t('employee.department'),
        enableSorting: false,
        // 216px — xodimlar ro'yxatidagi shu ustun bilan bir xil qiymat:
        // bo'lim nomi bir xil katalogdan keladi, demak bir xil joy kerak.
        meta: { width: '216px', hideOnMobile: true },
        cell: ({ getValue }) => <Muted value={getValue()} />,
      }),

      helper.accessor('startDate', {
        header: t('probation.startDate'),
        meta: { width: '130px', hideOnMobile: true },
        cell: ({ getValue }) => <DateText value={getValue()} />,
      }),

      helper.accessor('endDate', {
        header: t('probation.endDate'),
        meta: { width: '130px' },
        cell: ({ getValue }) => <DateText value={getValue()} />,
      }),

      helper.accessor('daysLeft', {
        header: t('probation.daysLeft'),
        /*
          150px ikkinchi qatorga tushirardi: muddati o'tgan holat oddiy
          "12 kun" emas, to'liq jumla — ruscha "просрочено на 12 дн."
          belgi bilan birga eng uzun matn. 210px shu jumlani bir qatorda
          ushlab turadi, qolgan holatlar (qisqa "N kun") esa erkin joy
          qoladi.
        */
        meta: { width: '210px' },
        cell: ({ row }) => <DaysLeft item={row.original} />,
      }),

      helper.accessor('status', {
        header: t('employee.status'),
        meta: { width: '150px' },
        cell: ({ row }) => (
          <Badge tone={STATUS_TONE[row.original.status]} size="sm">
            {t(`probation.status.${row.original.status}`)}
          </Badge>
        ),
      }),

      helper.display({
        id: 'actions',
        header: '',
        // 210px — ikkita tugma yonma-yon: "Uzaytirish"/"Продлить" va
        // "Yakunlash"/"Завершить". Ruscha va o'zbekcha kirill juftligi
        // eng keng, 190px ularni bir-biriga qisib qo'yardi.
        meta: { width: '210px', align: 'end' },
        cell: ({ row }) => {
          const item = row.original

          // Yakunlangan muddatni qayta yakunlab yoki uzaytirib bo'lmaydi —
          // domen ham buni rad etadi. Tugmani ko'rsatib, keyin xato
          // chiqarish foydalanuvchini aldash bo'lardi.
          if ((!canExtend && !canComplete) || item.status !== 'inProgress') return null

          return (
            /*
              ⚠️ Bosish qatorga O'TMAYDI. Qator endi xodim kartasiga olib
              boradi, tugmalar esa oyna ochadi — to'sisiz ikkalasi birga
              ishga tushardi va HR «Uzaytirish» ni bosib, o'zini boshqa
              ekranda topardi.
            */
            <span
              className="flex justify-end gap-1.5"
              onClick={(event) => event.stopPropagation()}
            >
              {canExtend && (
                <Button size="sm" variant="secondary" onClick={() => setExtending(item)}>
                  {t('probation.extend')}
                </Button>
              )}
              {canComplete && (
                <Button size="sm" onClick={() => setResolving(item)}>
                  {t('probation.resolve')}
                </Button>
              )}
            </span>
          )
        },
      }),
    ],
    [canExtend, canComplete, t],
  )

  return (
    <div data-list-frame className="space-y-5 frame:flex frame:h-full frame:flex-col">
      {/*
        «Yangi xodim qabul qilish» tugmasi AYNAN shu sahifada turadi.

        Sabab oqimda: har bir yangi odam istisnosiz sinovdan boshlaydi
        (`Probation.MinDays`), ya'ni qabul qilingan zahoti u shu ro'yxatga
        tushadi. Ilgari tugma `/xodimlar` da edi — doimiy xodimlar
        ro'yxatida — va HR odam qo'shgach uni o'sha ro'yxatda ko'rmasdi.

        Oynaning O'ZI xodimlar modulida qoldi (`hire-dialog.tsx`): u shaxs,
        mehnat munosabati va ma'lumotnomalar bilan ishlaydi, sinov esa
        uning natijasi. Ko'chirilgani — chaqiruv joyi, kod emas.
      */}
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={Timer} tone="orange" />
            {t('probation.title')}
          </span>
        }
        subtitle={list.isSuccess ? t('probation.subtitle', { n: num(total) }) : undefined}
        actions={
          canHire ? (
            <Button variant="create" onClick={() => setHireOpen(true)}>
              <UserPlus />
              {t('employees.hire')}
            </Button>
          ) : undefined
        }
      />

      {soonCount > 0 && (
        <Alert tone="warning" title={t('probation.endingSoonTitle', { n: num(soonCount) })}>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <span>{t('probation.endingSoonText', { days: WARNING_DAYS })}</span>

            {status !== 'inProgress' && (
              <Button size="sm" variant="secondary" onClick={() => setStatus('inProgress')}>
                {t('probation.showActive')}
              </Button>
            )}
          </div>
        </Alert>
      )}

      {/*
        Ko'chirishdan qolgan «yo'qolgan» yozuvlar bloki — jadvaldan YUQORIDA.
        Ular hech qaysi ro'yxatda ko'rinmaydi, ya'ni HR ularni faqat shu
        yerda topadi; pastda tursa, e'tibordan chetda qolardi.
        Ro'yxat bo'shagach blok butunlay yo'qoladi.
      */}
      <MissingProbations canConfirm={canComplete} />

      <Card className="frame:flex frame:flex-1 frame:flex-col">
        <div className="flex flex-wrap items-center gap-2.5 border-b border-[var(--border)] p-4 sm:gap-3">
          {/*
            Qidiruv birinchi va butun bo'sh kenglikni egallaydi: sinovdagi
            ikkita bir xil familiyani ajratish uchun HR JSHSHIR yozadi,
            holatni esa kamdan-kam o'zgartiradi.
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

          <Select
            options={statusOptions}
            value={status}
            onChange={(event) => {
              setPage(1)
              setStatus(event.target.value as ProbationFilters['status'])
            }}
            className="w-full sm:w-[260px]"
          />

          {/*
            «Filtrlar» — sana oralig'i yig'ilgan panelda (`IncentivesToolbar`
            naqshi): qidiruv va holat kundalik, oraliq esa kamdan-kam
            o'zgaradi va doim ochiq tursa ro'yxatni pastga surardi.
          */}
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
        </div>

        {rangeOpen && (
          <div className="border-b border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {/* Oraliq sinov TUGASH sanasi bo'yicha (`endDate`) — yorliq shuni aytadi */}
              <DateField
                label={t('probation.endFrom')}
                value={range.from}
                max={range.to === '' ? undefined : range.to}
                onChange={(value) => changeRange({ ...range, from: value })}
              />
              <DateField
                label={t('probation.endTo')}
                value={range.to}
                min={range.from === '' ? undefined : range.from}
                onChange={(value) => changeRange({ ...range, to: value })}
              />
            </div>

            {hasDateRange(range) && (
              <div className="mt-3.5 flex justify-end">
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  onClick={() => changeRange(NO_DATE_RANGE)}
                >
                  <X />
                  {t('employees.clearFilters')}
                </Button>
              </div>
            )}
          </div>
        )}

        {rangeInvalid && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="warning">{errorMessage('royxat.sana_oraligi_notogri')}</Alert>
          </div>
        )}

        <DataTable
          fill
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          loading={list.isPending && !rangeInvalid}
          onRowClick={openEmployee}
          rowLabel={(row) => row.employeeName}
          empty={
            // Teskari oraliqda bo'sh holat chizilmaydi — sabab ogohlantirishda.
            // Oraliq qo'yilgan bo'lsa — «topilmadi»: «sinovda hech kim yo'q»
            // va «qabul qilish» tugmasi bu yerda yolg'on bo'lardi.
            rangeInvalid ? undefined : hasDateRange(range) ? (
              <EmptyState
                icon={SearchX}
                title={t('common.empty')}
                description={t('common.dateRangeEmptyText')}
              />
            ) : (
              <EmptyState
                icon={CalendarClock}
                title={t('probation.emptyTitle')}
                description={t('probation.emptyText')}
                /*
                  Bo'sh ekran — aynan «boshlash» payti: sinovda hech kim yo'q
                  bo'lsa, HR bu yerga yangi odam qo'shish uchun keladi. Tugma
                  yuqorida ham bor, lekin bo'sh jadval o'rtasida ko'z birinchi
                  shu yerga tushadi.
                */
                action={
                  canHire ? (
                    <Button variant="create" onClick={() => setHireOpen(true)}>
                      <UserPlus />
                      {t('employees.hire')}
                    </Button>
                  ) : undefined
                }
              />
            )
          }
        />

        {/* `total > 0`: hajm tanlovi kichik ro'yxatda ham topilsin (`Pagination`) */}
        {total > 0 && (
          <Pagination
            page={page}
            pageSize={pageSize}
            total={total}
            onPageChange={setPage}
            onPageSizeChange={(next) => {
              setPageSize(next)
              setPage(1)
            }}
            allowAll={allowAll}
            disabled={list.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>

      <ResolveDialog item={resolving} onClose={() => setResolving(null)} />
      <ExtendDialog item={extending} onClose={() => setExtending(null)} />
      <HireDialog open={hireOpen} onClose={() => setHireOpen(false)} options={options} />
    </div>
  )
}

function Muted({ value }: { value: string | null }) {
  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="truncate-1 text-[var(--fg-secondary)]">{value}</span>
}

function DateText({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}

/**
 * Qolgan kun — ekrandagi eng muhim ustun.
 *
 * Uch holat uch xil ko'rinadi va rang yagona signal EMAS: matn ham
 * o'zgaradi. Rangni ajratmaydigan foydalanuvchi ham «muddat o'tdi» ni
 * o'qiy olishi kerak.
 */
function DaysLeft({ item }: { item: ProbationItem }) {
  const { t, num } = useLocale()

  if (item.status !== 'inProgress') {
    return <span className="text-[var(--fg-tertiary)]">—</span>
  }

  if (item.daysLeft < 0) {
    return (
      <span className="flex items-center gap-1.5 font-medium text-ios-red">
        <CircleAlert className="size-4 shrink-0" aria-hidden />
        {t('probation.overdue', { n: num(Math.abs(item.daysLeft)) })}
      </span>
    )
  }

  return (
    <span
      className={
        item.needsWarning
          ? 'flex items-center gap-1.5 font-medium text-ios-orange'
          : 'tabular text-[var(--fg-secondary)]'
      }
    >
      {item.needsWarning && <CircleAlert className="size-4 shrink-0" aria-hidden />}
      {t('probation.daysValue', { n: num(item.daysLeft) })}
    </span>
  )
}
