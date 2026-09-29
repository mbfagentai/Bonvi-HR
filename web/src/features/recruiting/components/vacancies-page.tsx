import { getRouteApi } from '@tanstack/react-router'
import {
  BriefcaseBusiness,
  CircleSlash,
  LoaderCircle,
  Pencil,
  Plus,
  RotateCcw,
  SearchX,
  Trash2,
} from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import type { SortingState } from '@tanstack/react-table'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  Pagination,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import type { Vacancy } from '../api/recruiting-api'
import { useChangeVacancyStatus, useVacancies } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { RECRUITING_MANAGE, VACANCY_LOOK } from '../lib/recruiting-presentation'
import {
  DEFAULT_SORT,
  filtersToSearch,
  isFiltered,
  normalizeSort,
  searchToFilters,
  type VacanciesFilterState,
  type VacanciesSearch,
} from '../lib/vacancies-search'
import { VacanciesToolbar } from './vacancies-toolbar'
import { VacancyDeleteDialog } from './vacancy-delete-dialog'
import { VacancyFormDialog } from './vacancy-form-dialog'

const helper = dataColumnHelper<Vacancy>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: Vacancy[] = []

/**
 * Bir sahifadagi yozuvlar soni.
 *
 * 25 — xodimlar (50) va buyruqlardan (50) KAM va bu ataylab:
 * vakansiyalar ro'yxati o'nlab qatordan iborat bo'ladi, yuzlab emas
 * (kompaniyada bir vaqtda 5–20 ta ochiq o'rin). Katta sahifa hajmi
 * bu yerda faqat bo'sh joyni tortadi.
 */
const PAGE_SIZE = 25

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/vakansiyalar')

/**
 * ⭐ Vakansiyalar — voronkaning boshlanish nuqtasi.
 *
 * Har bir nomzod aynan bitta vakansiyaga tegishli: «bu odam nimaga
 * kelgan» degan savolga javob shu ro'yxatdan boshlanadi
 * (`Vacancy` domen izohi).
 *
 * <b>Qatorda IKKITA sanoq bor va ular boshqa-boshqa savolga javob
 * beradi.</b> «Nomzodlar» — voronkaga nechta odam kirgani (mehnat
 * hajmi), «to'ldirildi» — nechtasi ishga olingani (natija). Vakansiya
 * o'rin to'lganda O'ZI YOPILMAYDI: `openings` reja, fakt emas — yopish
 * qarori HR niki (domen izohi). Shu sababli ikkala raqam ham ko'rinib
 * turadi va qaror uchun ma'lumot beradi.
 *
 * <b>Qator bosilganda TAHRIRLASH emas, NOMZODLAR ochiladi.</b>
 * Ma'lumotnomalarda qator bosilishi tahrirlash oynasini ochadi
 * (`catalog-page.tsx`), lekin vakansiya ma'lumotnoma emas — u ish
 * jarayoni. HR kuniga o'nlab marta «bu o'ringa kimlar bor?» deb
 * qaraydi va yiliga bir marta e'lon nomini tuzatadi. Tahrirlash esa
 * alohida ikona bilan qoladi.
 */
export function VacanciesPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(RECRUITING_MANAGE)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const page = search.page ?? 1
  const sort = search.sort ?? DEFAULT_SORT
  const filters = useMemo(() => searchToFilters(search), [search])

  const [editing, setEditing] = useState<Vacancy | null>(null)
  const [creating, setCreating] = useState(false)
  const [deleting, setDeleting] = useState<Vacancy | null>(null)

  /*
    Qidiruv matni serverga QIRQILGAN holda ketadi: manzilda u qirqilmaydi
    (`vacancies-search.ts` dagi `readText` izohi), lekin «Sotuvchi » va
    «Sotuvchi» bir xil so'rov — ikkita kesh yozuvi hosil qilmasin.
  */
  const searchText = filters.search.trim()

  const query = useVacancies({
    ...(filters.status === '' ? {} : { status: filters.status }),
    ...(searchText === '' ? {} : { search: searchText }),
    page,
    pageSize: PAGE_SIZE,
    sort,
  })

  /** Standart holatda `replace` — filtr aniqlashtirish, alohida qadam emas. */
  const setSearch = useCallback(
    (
      next: (previous: VacanciesSearch) => VacanciesSearch,
      history: 'push' | 'replace' = 'replace',
    ) => {
      void navigate({ to: '/vakansiyalar', search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  /** Filtr o'zgarganda 7-sahifada qolib ketmaslik kerak — u endi bo'sh. */
  const changeFilters = useCallback(
    (next: VacanciesFilterState) => {
      setSearch((previous) => ({ ...previous, ...filtersToSearch(next), page: undefined }))
    },
    [setSearch],
  )

  const changePage = useCallback(
    (next: number) => {
      setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push')
    },
    [setSearch],
  )

  /**
   * SERVER tomonda saralash: jadval o'zi tartiblasa, u faqat joriy
   * 25 qatorni saralab, butun ro'yxat saralandi degan yolg'on taassurot
   * qoldirardi (`DataTable.sorting` izohi).
   */
  const sorting = useMemo<SortingState>(
    () => [{ id: sort.replace('-', ''), desc: sort.startsWith('-') }],
    [sort],
  )

  const changeSorting = useCallback(
    (next: SortingState) => {
      const first = next[0]
      const raw = first === undefined ? undefined : `${first.desc ? '-' : ''}${first.id}`

      setSearch((previous) => ({ ...previous, sort: normalizeSort(raw), page: undefined }))
    },
    [setSearch],
  )

  /*
    Yopish / qayta ochish — BIR BOSISHDA, tasdiqlash oynasisiz.

    NEGA oyna yo'q: ikkala amal ham qaytariladi va nomzodlarga tegmaydi
    — yopilgan vakansiya faqat yangi nomzod qabul qilmaydi. Ilgari oyna
    sana va sabab so'rardi; egasining qarori bilan vakansiya eng sodda
    shaklga keltirildi va server yopilgan sanani o'zi qo'yadi (bugun).

    Xato (masalan qayta ochishda «vakansiya.nom_band» — shu nomli ochiq
    vakansiya allaqachon bor) sahifaning tepasida, ro'yxat xatosi bilan
    bir joyda chiqadi: amal oynasiz, ya'ni xatoni ko'rsatadigan boshqa
    joy yo'q. Keyingi bosishda o'zi yo'qoladi.

    Bitta amal ketayotganda BOSHQA qatorlardagi holat tugmalari ham
    o'chiq turadi: bir vaqtda faqat bitta so'rov — shunda xato qaysi
    vakansiyaga tegishli ekani hech qachon aralashmaydi.
  */
  const changeStatus = useChangeVacancyStatus()
  const mutateStatus = changeStatus.mutate
  const statusPending = changeStatus.isPending
  const statusTargetId = changeStatus.isPending ? changeStatus.variables.id : null
  const [statusFailure, setStatusFailure] = useState<{ code: string; item: Vacancy } | null>(
    null,
  )

  const toggleStatus = useCallback(
    (item: Vacancy) => {
      setStatusFailure(null)
      mutateStatus(
        { id: item.id, action: item.status === 'Closed' ? 'reopen' : 'close' },
        { onError: (error) => setStatusFailure({ code: recruitingErrorCode(error), item }) },
      )
    },
    [mutateStatus],
  )

  /** Vakansiyaning nomzodlari — bitta bosishda filtrlangan ro'yxat. */
  const openCandidates = useCallback(
    (vacancy: Vacancy) => {
      void navigate({ to: '/nomzodlar', search: { vacancy: vacancy.id } })
    },
    [navigate],
  )

  const columns = useMemo<Array<DataColumn<Vacancy>>>(() => {
    const definitions: Array<DataColumn<Vacancy>> = [
      /*
        Ustun sarlavhasi «Lavozim» EMAS, «Vakansiya nomi»: bu e'londa
        turadigan nom va u kompaniya lavozimi bilan ataylab
        aralashtirilmaydi — lavozim ishga qabulda tanlanadi.

        Ikkinchi qator (bo'lim) ham OLIB TASHLANDI — vakansiyada bo'lim
        yo'q. Bir xil nomli eski e'lonlarni (server yangi takrorga yo'l
        qo'ymaydi — «vakansiya.nom_band») ajratadigan narsa «Ochilgan»
        ustuni.
      */
      helper.accessor('title', {
        header: t('recruiting.vacancyTitle'),
        /*
          YAGONA `minWidth`li ustun — qoidaga ko'ra jadvalda bittadan
          ortiq bo'lmasligi kerak (`data-table.tsx`). E'lon nomlarining
          uzunligi oldindan noma'lum, shu sababli ortgan joyni aynan shu
          ustun oladi.
        */
        meta: { minWidth: '260px' },
        cell: ({ row }) => <p className="truncate-1 font-medium">{row.original.title}</p>,
      }),

      helper.accessor('status', {
        header: t('recruiting.status'),
        enableSorting: false,
        // 110px: holat ikkitaga qisqargach eng uzun tarjima
        // «Yopilgan» / «Закрыта» (8 belgi) bo'lib qoldi — ilgari
        // 130px «To'xtatilgan» / «Приостановлена» (12 belgi) uchun
        // olingan edi va u yorliq endi yo'q.
        meta: { width: '110px' },
        cell: ({ row }) => {
          const look = VACANCY_LOOK[row.original.status]
          return (
            <Badge tone={look.tone} size="sm">
              {t(`recruiting.vacancyStatus.${look.key}`)}
            </Badge>
          )
        },
      }),

      helper.display({
        id: 'progress',
        header: t('recruiting.filled'),
        // 110px — «12/20» eng uzun holatda ham 5 belgi (tabular).
        meta: { width: '110px' },
        cell: ({ row }) => (
          <span
            className="tabular text-subhead"
            title={t('recruiting.filledHint', {
              hired: num(row.original.hiredCount),
              openings: num(row.original.openings),
            })}
          >
            <span className="font-semibold">{num(row.original.hiredCount)}</span>
            <span className="text-[var(--fg-tertiary)]">
              {' / '}
              {num(row.original.openings)}
            </span>
          </span>
        ),
      }),

      helper.accessor('candidateCount', {
        header: t('nav.candidates'),
        enableSorting: false,
        meta: { width: '120px' },
        cell: ({ getValue }) => (
          <span className="tabular text-subhead">
            {getValue() === 0 ? (
              <span className="text-[var(--fg-tertiary)]">—</span>
            ) : (
              num(getValue())
            )}
          </span>
        ),
      }),

      helper.accessor('openedAt', {
        header: t('recruiting.openedAt'),
        // 130px — probations va buyruqlardagi sana ustunlari bilan bir
        // xil: «25.08.2026» (10 belgi, tabular) + sarlavha sig'adi.
        meta: { width: '130px' },
        /*
          Yopilgan sana bu yerda KO'RSATILMAYDI, garchi u DTO da bo'lsa
          ham: ustun sarlavhasi «Ochilgan» deydi va ikkita turli faktni
          bitta katakda almashtirib ko'rsatish qatorni yolg'on qilardi.
          Yopilganini holat yorlig'i allaqachon aytib turibdi.
        */
        cell: ({ getValue }) => <DateCell value={getValue()} />,
      }),

      /*
        «Yuridik shaxs» ustuni O'CHIRILDI: vakansiyada firma yo'q. Xodim
        qaysi firmaga rasmiylashtirilgani ishga qabulda hal qilinadi va
        xodim kartasida ko'rinadi — vakansiya ro'yxatida esa u har doim
        bo'sh katak bo'lardi. «Mas'ul» ustuni ham shu sababdan yo'q:
        mas'ul xodim vakansiyadan butunlay olib tashlandi.
      */
    ]

    // Ruxsat yo'q bo'lsa ustunning O'ZI ham chiqmaydi — bo'sh ustun
    // «bu yerda nimadir bo'lishi kerak edi» degan taassurot qoldiradi
    // (`catalog-page.tsx` dagi bilan bir xil qoida).
    if (canManage) {
      definitions.push(
        helper.display({
          id: 'actions',
          header: '',
          /*
            120px: ko'pi bilan UCHTA `icon-sm` tugma (32px) + oraliqlar.
            Ilgari 152px edi — to'rtta tugma uchun. Kenglik QAT'IY
            (eng ko'p tugmaga qarab): qatordan qatorga o'zgarsa,
            o'chirish tugmasi bor-yo'qligiga qarab butun ustun
            siljib ketardi va ko'z tayanadigan chekka yo'qolardi.
          */
          meta: { align: 'end', width: '120px' },
          cell: ({ row }) => (
            <RowActions
              item={row.original}
              statusBusy={statusTargetId === row.original.id}
              statusLocked={statusPending}
              onToggleStatus={() => toggleStatus(row.original)}
              onEdit={() => setEditing(row.original)}
              onDelete={() => setDeleting(row.original)}
            />
          ),
        }),
      )
    }

    return definitions
  }, [canManage, num, statusPending, statusTargetId, t, toggleStatus])

  const rows = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0
  const firstLoad = query.isPending
  const filtered = isFiltered(search)
  const listEmpty = rows.length === 0 && !firstLoad

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={BriefcaseBusiness} tone="brand" size="lg" />
            {t('nav.vacancies')}
          </span>
        }
        subtitle={
          firstLoad ? t('common.loading') : t('recruiting.vacancySubtitle', { n: num(total) })
        }
        actions={
          canManage ? (
            <Button variant="create" onClick={() => setCreating(true)}>
              <Plus />
              {t('recruiting.vacancyCreate')}
            </Button>
          ) : undefined
        }
      />

      {query.isError && (
        <Alert tone="danger">{errorMessage(recruitingErrorCode(query.error))}</Alert>
      )}

      {/*
        Sarlavha — vakansiya NOMI: xato ro'yxatning tepasida chiqadi va
        usiz qaysi qatorda bosilgani noma'lum qolardi.

        «vakansiya.nom_band» bu yerda O'Z MATNI bilan: umumiy tarjima
        forma uchun yozilgan («ro'yxatdan toping va qayta oching») va
        qayta ochish paytida u teskari maslahat bo'lardi — HR aynan
        qayta ochmoqchi bo'lgan, band qilgan esa boshqa, OCHIQ vakansiya.
      */}
      {statusFailure !== null && (
        <Alert tone="danger" title={statusFailure.item.title}>
          {statusFailure.code === 'vakansiya.nom_band'
            ? t('recruiting.vacancyReopenNameTaken')
            : errorMessage(statusFailure.code)}
        </Alert>
      )}

      <Card className="overflow-hidden">
        <VacanciesToolbar filters={filters} onChange={changeFilters} />

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(item) => item.id}
          loading={firstLoad}
          skeletonRows={6}
          onRowClick={openCandidates}
          rowLabel={(item) => item.title}
          sorting={sorting}
          onSortingChange={changeSorting}
          empty={
            listEmpty ? (
              filtered ? (
                <EmptyState
                  icon={SearchX}
                  title={t('recruiting.vacancyNotFoundTitle')}
                  description={t('recruiting.vacancyNotFoundText')}
                />
              ) : (
                <EmptyState
                  icon={BriefcaseBusiness}
                  title={t('recruiting.vacancyEmptyTitle')}
                  description={t('recruiting.vacancyEmptyText')}
                  action={
                    canManage ? (
                      <Button variant="create" onClick={() => setCreating(true)}>
                        <Plus />
                        {t('recruiting.vacancyCreate')}
                      </Button>
                    ) : undefined
                  }
                />
              )
            ) : null
          }
        />

        {total > 0 && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            total={total}
            onPageChange={changePage}
            disabled={query.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>

      <VacancyFormDialog
        open={creating || editing !== null}
        item={editing}
        onClose={() => {
          setCreating(false)
          setEditing(null)
        }}
      />

      <VacancyDeleteDialog item={deleting} onClose={() => setDeleting(null)} />
    </div>
  )
}

/**
 * Qator amallari — ko'pi bilan UCHTA tugma.
 *
 * NEGA ochiluvchi menyu emas: `shared/ui` da menyu primitivi yo'q va
 * uni shu ish uchun qo'shish qoidaga zid. Ikonalar esa amallar
 * SONI KAM bo'lganda menyudan tez: bir bosish, ikki emas.
 *
 * <b>Yopish va qayta ochish — BITTA tugma, ikki tomonga</b> va u
 * oynasiz ishlaydi: bosildi — holat almashdi. Ikkalasi bir-birining
 * teskarisi, ya'ni ular hech qachon birga kerak bo'lmaydi va bitta
 * joyni bo'lishadi. Ikona ham, matn ham holatga qarab almashadi; so'rov
 * ketayotganda ikona o'rnida aylana turadi.
 *
 * <b>«O'chirish» nomzodi bor qatorda UMUMAN chizilmaydi</b> —
 * o'chirilgan holatda emas, butunlay yo'q. Sabab: o'chirilgan tugma
 * «nega bosilmaydi?» degan savol tug'diradi, yo'q tugma esa savol
 * tug'dirmaydi. Nomzodi bor vakansiya — tarix va uni o'chirish
 * nomzodlarning yozuvini yetim qoldirardi; server buni qat'iy tekshiradi
 * («vakansiya.nomzodlari_bor»), bu yerdagi shart esa faqat foydalanuvchi
 * bekorga urinmasin degani. `candidateCount` ro'yxat javobida ALLAQACHON
 * bor, ya'ni bu shart uchun qo'shimcha so'rov kerak emas.
 *
 * `stopPropagation` majburiy va u BUTUN katakda, har bir tugmada emas:
 * qatorning O'ZI bosiladi (nomzodlarga o'tadi), tugmalar esa uning ichida
 * turadi. O'chiq tugma (`disabled` — `pointer-events-none`) bosishni
 * o'zi ushlamaydi va u ostidagi qatorga tushardi — ya'ni holat
 * o'zgarayotganda tugmani yana bosgan HR kutilmaganda nomzodlar
 * ro'yxatiga o'tib ketardi. Klaviatura ham shu sababli to'xtatiladi:
 * qator Enter'ni o'zi ushlaydi va tugma o'rniga nomzodlarni ochardi.
 */
function RowActions({
  item,
  statusBusy,
  statusLocked,
  onToggleStatus,
  onEdit,
  onDelete,
}: {
  item: Vacancy
  /** Aynan SHU qatorning holati o'zgarmoqda — aylana ko'rsatiladi. */
  statusBusy: boolean
  /** Biror qatorning holati o'zgarmoqda — tugma bosilmaydi. */
  statusLocked: boolean
  onToggleStatus: () => void
  onEdit: () => void
  onDelete: () => void
}) {
  const { t } = useLocale()

  const closed = item.status === 'Closed'

  return (
    <div
      className="flex items-center justify-end"
      onClick={(event) => event.stopPropagation()}
      onKeyDown={(event) => event.stopPropagation()}
    >
      <ActionButton
        icon={statusBusy ? LoaderCircle : closed ? RotateCcw : CircleSlash}
        spin={statusBusy}
        disabled={statusLocked}
        label={t(
          closed
            ? 'recruiting.vacancyAction.reopenTitle'
            : 'recruiting.vacancyAction.closeTitle',
        )}
        onClick={onToggleStatus}
      />

      <ActionButton icon={Pencil} label={t('common.edit')} onClick={onEdit} />

      {item.candidateCount === 0 && (
        <ActionButton icon={Trash2} label={t('common.delete')} danger onClick={onDelete} />
      )}
    </div>
  )
}

function ActionButton({
  icon: Icon,
  label,
  onClick,
  danger = false,
  spin = false,
  disabled = false,
}: {
  icon: typeof Pencil
  label: string
  onClick: () => void
  danger?: boolean
  spin?: boolean
  disabled?: boolean
}) {
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      title={label}
      aria-label={label}
      disabled={disabled}
      onClick={onClick}
    >
      <Icon
        className={cn(danger && 'text-[var(--danger-text)]', spin && 'animate-spin')}
      />
    </Button>
  )
}

function DateCell({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null || value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
