import { getRouteApi } from '@tanstack/react-router'
import {
  DoorOpen,
  FileSpreadsheet,
  ListFilter,
  LoaderCircle,
  Search,
  SearchX,
  X,
} from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { isRateLimited } from '@/features/employees/lib/employee-error'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { toSearchParam, useSearchDraft } from '@/shared/lib/list-search'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Pagination,
  Select,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import {
  PASS_DIRECTIONS,
  PASS_METHODS,
  type AttendancePass,
  type PassDirection,
  type PassMethod,
} from '../api/attendance-api'
import {
  useAttendanceDevices,
  useAttendancePasses,
  useExportPasses,
} from '../api/attendance-queries'
import {
  ATTENDANCE_MANAGE,
  ATTENDANCE_VIEW,
  DEFAULT_PAGE_SIZE,
  DIRECTION_LOOK,
  METHOD_LABEL_KEY,
  REPORTS_EXPORT,
  formatPassTime,
} from '../lib/attendance-presentation'
import {
  DEFAULT_PASS_FILTERS,
  isRangeInverted,
  isRangeTooLong,
  passFiltersToQuery,
  type PassFilterState,
} from '../lib/attendance-search'
import { MobileEvidenceLine } from './attendance-mobile-evidence'
import {
  AttendancePassPhotoDialog,
  PassDirectionBadge,
  PassFacePair,
  PassFlags,
} from './attendance-pass-photo-dialog'

const helper = dataColumnHelper<AttendancePass>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: AttendancePass[] = []

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`incidents-page.tsx` dagi izoh: marshrut fayli shu komponentni import qiladi).
 */
const route = getRouteApi('/_app/verifikatsiya/otishlar')

/**
 * Sukut holati — BUGUN.
 *
 * Server ham sanasiz so'rovni «bugun» deb oladi (`PeriodDefault.SingleDay`),
 * lekin sana maydonlari bo'sh tursa HR ekranda NIMA ko'rayotganini
 * bilmasdi: «hammasi» deb o'ylab, kechagi o'tishni topolmay qolardi.
 * Oraliq maydonlarda ochiq yozilgan bo'lsa, savol tug'ilmaydi.
 *
 * Funksiya, konstanta emas: sahifa yarim tundan keyin ham ochiq turishi
 * mumkin va «bugun» sahifa ochilgan paytda hisoblanishi kerak.
 */
function defaultFilters(): PassFilterState {
  const today = todayIso()
  return { ...DEFAULT_PASS_FILTERS, from: today, to: today }
}

/**
 * O'tishlar jurnali — qurilma NIMA DEGAN bo'lsa, o'shanday (SPEC §8.4).
 *
 * ⭐ Har bir qatorda turniket surati xodim kartasidagi rasm BILAN
 * yonma-yon turadi — bo'limning nomi shundan: HR yuzni bir qarashda
 * tasdiqlaydi. Qator bosilsa ikkalasi katta oynada ochiladi.
 *
 * Holat qayerda yashaydi:
 *   · qidiruv matni va `passId` — MANZILDA (`validatePassesSearch`):
 *     taxtadagi «ishdan ketgan xodim» ogohlantirishi aynan `?passId=`
 *     bilan shu yerga olib keladi;
 *   · sana, qurilma, yo'nalish va boshqa filtrlar — sahifaning o'zida
 *     (`hodisalar.tsx` da qabul qilingan yechim, `attendance-search.ts`).
 */
export function AttendancePassesPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canView = hasPermission(ATTENDANCE_VIEW)
  const canManage = hasPermission(ATTENDANCE_MANAGE)
  const canExport = hasPermission(REPORTS_EXPORT)
  // Xodim kartasi `employees.view` bilan himoyalangan — ruxsatsiz tugma chizilmaydi
  const canOpenEmployee = hasPermission(EMPLOYEES_VIEW)

  const searchParams = route.useSearch()
  const navigate = route.useNavigate()

  const search = searchParams.q ?? ''

  const [filters, setFilters] = useState<PassFilterState>(defaultFilters)
  const [page, setPage] = useState(1)
  const [panelOpen, setPanelOpen] = useState(false)
  /** Qatordan tanlangan o'tish — oyna so'rovsiz ochiladi. */
  const [selected, setSelected] = useState<AttendancePass | null>(null)

  /*
   * Qidiruv har harfda so'rov yubormaydi (350 ms) va matn manzilga
   * yoziladi. `replace` — qidiruvni aniqlashtirish tarix qadami emas.
   * `passId` saqlanadi: ochiq oyna qidiruv yozilgani uchun yopilmasin.
   */
  const [draft, setDraft] = useSearchDraft(search, (value) => {
    setPage(1)
    void navigate({
      search: (previous) => ({ ...previous, q: toSearchParam(value) }),
      replace: true,
    })
  })

  const options = useFilterOptions()

  /*
    Qurilmalar ro'yxati `attendance.manage` ostida (SPEC §7.3: unda token
    prefikslari va binoning kirish joylari bor). Faqat ko'ra oladigan
    foydalanuvchida so'rov UMUMAN yuborilmaydi va qurilma filtri
    chizilmaydi — bo'sh ro'yxatli tanlov «qurilma yo'q» deb yolg'on
    gapirardi.
  */
  const devices = useAttendanceDevices(true, canView && canManage)

  const inverted = isRangeInverted(filters.from, filters.to)
  const tooLong = !inverted && filters.from !== '' && filters.to !== '' && isRangeTooLong(filters.from, filters.to)
  const rangeProblem = inverted ? 'davomat.davr_notogri' : tooLong ? 'davomat.davr_uzun' : null

  /**
   * So'rov filtri — ro'yxat ham, Excel ham AYNAN shundan quriladi
   * («ekranda nima ko'rsang, faylda ham shu» — `employees-page.tsx`).
   */
  const queryFilters = useMemo(() => passFiltersToQuery(filters, search), [filters, search])

  /*
    Oraliq noto'g'ri bo'lsa so'rov YUBORILMAYDI: server baribir 400
    qaytaradi, ekran esa sababni tugma bosilishini kutmasdan maydon
    yonida aytadi.
  */
  const list = useAttendancePasses(
    { ...queryFilters, page, pageSize: DEFAULT_PAGE_SIZE },
    canView && rangeProblem === null,
  )
  const exportList = useExportPasses()

  const rows = list.data?.items ?? EMPTY
  const total = list.data?.total ?? 0
  const firstLoad = list.isPending && rangeProblem === null

  const changeFilters = useCallback((next: PassFilterState) => {
    setFilters(next)
    // Filtr o'zgarsa 7-sahifada qolib ketmaslik kerak — u endi bo'sh
    setPage(1)
  }, [])

  const set = <K extends keyof PassFilterState>(key: K, value: PassFilterState[K]) => {
    changeFilters({ ...filters, [key]: value })
  }

  /*
    Sana oralig'idan TASHQARI filtrlar soni — tugmadagi sanoq uchun. Sana
    birinchi qatorda ochiq turadi, ya'ni uni sanash «nega 1 turibdi?»
    degan savol tug'dirardi.
  */
  const panelCount = [
    filters.deviceId,
    filters.direction,
    filters.method,
    filters.matched,
    filters.legalEntityId,
    filters.departmentId,
  ].filter((value) => value !== '').length

  const today = todayIso()
  const isDefault =
    panelCount === 0 && search.trim() === '' && filters.from === today && filters.to === today

  const closeDialog = useCallback(() => {
    setSelected(null)
    if (searchParams.passId !== undefined) {
      // `replace` — yopilgan oyna tarixda qolmaydi: «orqaga» uni qayta ochmasin
      void navigate({
        search: (previous) => ({ ...previous, passId: undefined }),
        replace: true,
      })
    }
  }, [navigate, searchParams.passId])

  const columns = useMemo<Array<DataColumn<AttendancePass>>>(
    () => [
      helper.display({
        id: 'faces',
        // Ustun bo'limning nomini oladi: bu juftlik — «verifikatsiya»ning o'zi
        header: t('nav.verification'),
        /*
          Ikki 48 px rasm + 8 px oraliq = 104 px + katak to'ldirishi (32 px)
          = 136 px, kichik zaxira bilan 144 px. Ustun ENG CHAPDA: ko'z qatorga
          avval yuzdan kiradi, keyin ismni o'qiydi — tekshirish tartibi shu.
        */
        meta: { width: '144px' },
        cell: ({ row }) => <PassFacePair pass={row.original} />,
      }),

      helper.accessor('occurredAt', {
        header: t('attendance.time'),
        enableSorting: false,
        meta: { width: '104px' },
        cell: ({ row }) => <TimeCell value={row.original.occurredAt} />,
      }),

      helper.display({
        id: 'employee',
        header: t('attendance.employee'),
        meta: { minWidth: '240px' },
        cell: ({ row }) => <EmployeeCell pass={row.original} />,
      }),

      helper.accessor('deviceName', {
        header: t('attendance.device'),
        enableSorting: false,
        meta: { width: '200px', hideOnMobile: true },
        cell: ({ row }) => (
          <span className="block min-w-0 leading-tight">
            <span className="truncate-1 block text-[var(--fg-secondary)]">
              {row.original.deviceName ?? '—'}
            </span>
            {/* Telefon belgisi — obyekt, masofa va aniqlik (SPEC-xodim-ilovasi §F). */}
            <MobileEvidenceLine pass={row.original} />
          </span>
        ),
      }),

      helper.accessor('direction', {
        header: t('attendance.direction'),
        enableSorting: false,
        // «Noma'lum» (eng uzun yorliq) + ikonka + ichki bo'shliq ≈ 100 px + 32 px
        meta: { width: '136px' },
        cell: ({ row }) => <PassDirectionBadge
            direction={row.original.direction}
            inferred={row.original.directionSource === 'Alternation'}
          />,
      }),

      helper.accessor('method', {
        header: t('attendance.method'),
        enableSorting: false,
        meta: { width: '128px', hideOnMobile: true },
        cell: ({ row }) => (
          <span className="text-[var(--fg-secondary)]">
            {t(METHOD_LABEL_KEY[row.original.method])}
          </span>
        ),
      }),

      helper.display({
        id: 'flags',
        header: t('attendance.skewWarning'),
        meta: { width: '200px', hideOnMobile: true },
        cell: ({ row }) => <PassFlags pass={row.original} />,
      }),
    ],
    [t],
  )

  if (!canView) {
    return (
      <div className="space-y-6">
        <PageHeader title={<PageTitle />} />
        <Card>
          <EmptyState
            icon={DoorOpen}
            title={t('attendance.noAccessTitle')}
            description={t('attendance.noAccessText')}
          />
        </Card>
      </div>
    )
  }

  const directionOptions = PASS_DIRECTIONS.map((value) => ({
    value,
    label: t(DIRECTION_LOOK[value].labelKey),
  }))
  const methodOptions = PASS_METHODS.map((value) => ({
    value,
    label: t(METHOD_LABEL_KEY[value]),
  }))
  const deviceOptions = (devices.data ?? []).map((device) => ({
    value: device.id,
    label: device.name,
  }))

  const passId = selected?.id ?? searchParams.passId ?? null

  return (
    <div className="space-y-6">
      <PageHeader
        title={<PageTitle />}
        subtitle={
          firstLoad ? t('common.loading') : t('attendance.passesSubtitle', { n: num(total) })
        }
      />

      {list.isError && (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {errorMessage(problemCode(list.error))}
        </Alert>
      )}

      <Card className="overflow-hidden">
        <div className="border-b border-[var(--border)]">
          <div className="flex flex-wrap items-center gap-2.5 px-4 py-3.5 sm:gap-3 sm:px-5">
            <div className="min-w-[200px] flex-1">
              <Input
                icon={Search}
                shape="pill"
                type="search"
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                placeholder={t('attendance.passesSearchPlaceholder')}
                aria-label={t('common.search')}
              />
            </div>

            {/*
              Sana oralig'i PANELDA EMAS, birinchi qatorda: jurnalga har
              safar «qaysi kun?» degan savol bilan kiriladi. Qolgan filtrlar
              kamdan-kam o'zgaradi va yig'ilgan panelda turadi
              (`employees-toolbar.tsx` naqshi).
            */}
            <div className="flex items-center gap-2">
              {/* `Input` o'rami `w-full` — kenglik tashqi qutida beriladi,
                  aks holda ikki sana maydoni butun qatorni bo'lishib olardi */}
              <div className="w-[152px]">
                <Input
                  type="date"
                  value={filters.from}
                  max={filters.to === '' ? undefined : filters.to}
                  onChange={(event) => set('from', event.target.value)}
                  aria-label={t('common.dateFrom')}
                  title={t('common.dateFrom')}
                  className="tabular"
                />
              </div>
              <span aria-hidden className="text-[var(--fg-tertiary)]">
                –
              </span>
              <div className="w-[152px]">
                <Input
                  type="date"
                  value={filters.to}
                  min={filters.from === '' ? undefined : filters.from}
                  onChange={(event) => set('to', event.target.value)}
                  aria-label={t('common.dateTo')}
                  title={t('common.dateTo')}
                  className="tabular"
                />
              </div>
            </div>

            <Button
              type="button"
              pill
              variant={panelOpen ? 'soft' : 'secondary'}
              aria-expanded={panelOpen}
              onClick={() => setPanelOpen((previous) => !previous)}
            >
              <ListFilter />
              <span className="hidden sm:inline">{t('common.filters')}</span>
              {panelCount > 0 && (
                <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
                  {num(panelCount)}
                </span>
              )}
            </Button>

            {canExport && (
              <Button
                type="button"
                pill
                // Ikki marta bosilsa ikki fayl va tezlik cheklovi bekorga yeyiladi
                disabled={exportList.isPending || rangeProblem !== null}
                onClick={() => exportList.mutate(queryFilters)}
                title={t('attendance.export')}
                aria-label={t('attendance.export')}
              >
                {exportList.isPending ? <LoaderCircle className="animate-spin" /> : <FileSpreadsheet />}
                <span className="hidden lg:inline">
                  {exportList.isPending ? t('employees.exporting') : t('attendance.export')}
                </span>
              </Button>
            )}
          </div>

          {panelOpen && (
            <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                <Select
                  label={t('attendance.direction')}
                  placeholder={t('common.all')}
                  options={directionOptions}
                  value={filters.direction}
                  onChange={(event) => set('direction', event.target.value as PassDirection | '')}
                />
                <Select
                  label={t('attendance.method')}
                  placeholder={t('common.all')}
                  options={methodOptions}
                  value={filters.method}
                  onChange={(event) => set('method', event.target.value as PassMethod | '')}
                />
                <Select
                  label={t('attendance.matchStatus')}
                  placeholder={t('common.all')}
                  options={[
                    { value: 'matched', label: t('attendance.matched') },
                    { value: 'unmatched', label: t('attendance.unmatched') },
                  ]}
                  value={filters.matched}
                  onChange={(event) =>
                    set('matched', event.target.value as PassFilterState['matched'])
                  }
                />
                {canManage && (
                  <Select
                    label={t('attendance.device')}
                    placeholder={t('common.all')}
                    options={deviceOptions}
                    value={filters.deviceId}
                    onChange={(event) => set('deviceId', event.target.value)}
                  />
                )}
                <Select
                  label={t('employee.legalEntity')}
                  placeholder={t('employees.anyLegalEntity')}
                  options={options.legalEntities}
                  value={filters.legalEntityId}
                  onChange={(event) => set('legalEntityId', event.target.value)}
                />
                <Select
                  label={t('employee.department')}
                  placeholder={t('employees.anyDepartment')}
                  options={options.departments}
                  value={filters.departmentId}
                  onChange={(event) => set('departmentId', event.target.value)}
                />
              </div>

              {!isDefault && (
                <div className="mt-3.5 flex justify-end">
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    onClick={() => {
                      changeFilters(defaultFilters())
                      setDraft('')
                    }}
                  >
                    <X />
                    {t('employees.clearFilters')}
                  </Button>
                </div>
              )}
            </div>
          )}
        </div>

        {rangeProblem !== null && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="warning">{errorMessage(rangeProblem)}</Alert>
          </div>
        )}

        {/*
          Eksport xatosi tugmaning ostida, kartaning ichida
          (`employees-page.tsx` dagi sabab). 429 alohida aytiladi: uning
          yechimi — kutish, filtrni o'zgartirish emas.
        */}
        {exportList.isError && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="danger" title={t('employees.exportFailed')}>
              {isRateLimited(exportList.error)
                ? t('employees.exportTooOften')
                : errorMessage(problemCode(exportList.error))}
            </Alert>
          </div>
        )}

        <DataTable
          columns={columns}
          data={rangeProblem === null ? rows : EMPTY}
          getRowId={(pass) => pass.id}
          loading={firstLoad}
          skeletonRows={8}
          onRowClick={setSelected}
          rowLabel={(pass) => pass.employeeName ?? pass.rawPersonId}
          empty={
            rangeProblem !== null || list.isError ? undefined : isDefault ? (
              <EmptyState
                icon={DoorOpen}
                title={t('attendance.passesEmptyTitle')}
                description={t('attendance.passesEmptyText')}
              />
            ) : (
              <EmptyState
                icon={SearchX}
                title={t('common.empty')}
                description={t('attendance.notFoundText')}
              />
            )
          }
        />

        {total > 0 && rangeProblem === null && (
          <Pagination
            page={page}
            pageSize={DEFAULT_PAGE_SIZE}
            total={total}
            onPageChange={setPage}
            disabled={list.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>

      <AttendancePassPhotoDialog
        passId={passId}
        pass={selected}
        canOpenEmployee={canOpenEmployee}
        onClose={closeDialog}
      />
    </div>
  )
}

function PageTitle() {
  const { t } = useLocale()

  return (
    <span className="flex items-center gap-3">
      <IconTile icon={DoorOpen} tone="brand" size="lg" />
      {t('attendance.passesTitle')}
    </span>
  )
}

/**
 * Vaqt — katta, sana — ostida kichik. Ikkalasi ham TOSHKENT vaqtida
 * (`formatPassTime` / `date` bitta formatlagichdan).
 *
 * Sana har qatorda turadi: sukutda jurnal bitta kunni ko'rsatadi, lekin
 * oraliq kengaytirilganda qator qaysi kunga tegishli ekani ustundan
 * o'qilishi kerak.
 */
function TimeCell({ value }: { value: string }) {
  const { date } = useLocale()

  return (
    <span className="block leading-tight">
      <span className="tabular block text-subhead font-semibold">{formatPassTime(value)}</span>
      <span className="tabular mt-0.5 block text-caption2 text-[var(--fg-tertiary)]">
        {date(value)}
      </span>
    </span>
  )
}

/**
 * Xodim — yoki moslanmagan o'tishda qurilmadagi RAQAM.
 *
 * Raqam kulrang monospace yorliqda: bu ism emas va ism kabi o'qilmasligi
 * kerak. «Noma'lum shaxs» degan yozuv yonida — HR darhol «moslashtirish
 * kerak» deb tushunadi (SPEC §8.4).
 */
function EmployeeCell({ pass }: { pass: AttendancePass }) {
  const { t } = useLocale()

  if (pass.employeeId === null) {
    return (
      <span className="flex min-w-0 flex-col items-start gap-1">
        <span className="text-subhead text-[var(--fg-secondary)]">
          {t('attendance.unknownPerson')}
        </span>
        <Badge tone="neutral" size="sm" className="font-mono">
          {pass.rawPersonId}
        </Badge>
      </span>
    )
  }

  const org = [pass.department, pass.position].filter(Boolean).join(' · ')

  return (
    <span className="block min-w-0">
      <span className="truncate-1 block font-medium">{pass.employeeName}</span>
      {org !== '' && (
        <span className="truncate-1 mt-0.5 block text-caption1 text-[var(--fg-tertiary)]">
          {org}
        </span>
      )}
    </span>
  )
}
