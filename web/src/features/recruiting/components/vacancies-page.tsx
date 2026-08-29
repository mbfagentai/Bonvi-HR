import { getRouteApi } from '@tanstack/react-router'
import {
  BriefcaseBusiness,
  CircleSlash,
  PauseCircle,
  Pencil,
  Play,
  Plus,
  SearchX,
  Trash2,
} from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import type { SortingState } from '@tanstack/react-table'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
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
import { useVacancies } from '../api/recruiting-queries'
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
import { VacancyActionDialog, type VacancyAction } from './vacancy-action-dialog'
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
  const [action, setAction] = useState<{ kind: VacancyAction; item: Vacancy } | null>(null)

  const query = useVacancies({
    ...(filters.status === '' ? {} : { status: filters.status }),
    ...(filters.vacancyTitleId === '' ? {} : { vacancyTitleId: filters.vacancyTitleId }),
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
        aralashtirilmaydi (backend'dagi `VacancyTitle` domen izohi).

        Ikkinchi qator (bo'lim) ham OLIB TASHLANDI — vakansiyada bo'lim
        yo'q. Bir xil nomli ikkita ochiq e'lonni ajratadigan narsa
        endi «Ochilgan» ustuni.
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
        cell: ({ row }) => (
          <p className="truncate-1 font-medium">{row.original.title ?? '—'}</p>
        ),
      }),

      helper.accessor('status', {
        header: t('recruiting.status'),
        enableSorting: false,
        // 130px: eng uzun tarjima «To'xtatilgan» / «Приостановлена»
        // (12 belgi) `text-caption1` da sig'adi, sarlavha esa
        // `text-caption2` uppercase — undan ham qisqa.
        meta: { width: '130px' },
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
        bo'sh katak bo'lardi.
      */
      helper.accessor('responsibleName', {
        header: t('recruiting.responsible'),
        enableSorting: false,
        meta: { width: '180px', hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),
    ]

    // Ruxsat yo'q bo'lsa ustunning O'ZI ham chiqmaydi — bo'sh ustun
    // «bu yerda nimadir bo'lishi kerak edi» degan taassurot qoldiradi
    // (`catalog-page.tsx` dagi bilan bir xil qoida).
    if (canManage) {
      definitions.push(
        helper.display({
          id: 'actions',
          header: '',
          // 152px: to'rttagacha `icon-sm` tugma (32px) + oraliqlar.
          meta: { align: 'end', width: '152px' },
          cell: ({ row }) => (
            <RowActions
              item={row.original}
              onEdit={() => setEditing(row.original)}
              onAction={(kind) => setAction({ kind, item: row.original })}
            />
          ),
        }),
      )
    }

    return definitions
  }, [canManage, num, t])

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

      <Card className="overflow-hidden">
        <VacanciesToolbar filters={filters} onChange={changeFilters} />

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(item) => item.id}
          loading={firstLoad}
          skeletonRows={6}
          onRowClick={openCandidates}
          rowLabel={(item) => item.title ?? '—'}
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

      <VacancyActionDialog
        action={action?.kind ?? null}
        item={action?.item ?? null}
        onClose={() => setAction(null)}
      />
    </div>
  )
}

/**
 * Qator amallari.
 *
 * NEGA ochiluvchi menyu emas: `shared/ui` da menyu primitivi yo'q va
 * uni shu ish uchun qo'shish qoidaga zid. Ikonalar esa amallar
 * SONI KAM bo'lganda menyudan tez: bir bosish, ikki emas.
 *
 * Ro'yxat HOLATGA qarab qisqaradi — «to'xtatish» faqat ochiq
 * vakansiyada, «davom ettirish» faqat to'xtatilganida ko'rinadi.
 * Hammasini doim ko'rsatib, keraksizini o'chirib qo'yish (`disabled`)
 * mumkin edi, lekin u holda HR har safar to'rtta tugmadan qaysi biri
 * ishlashini o'qib chiqishi kerak bo'lardi.
 *
 * `stopPropagation` majburiy: qatorning O'ZI bosiladi (nomzodlarga
 * o'tadi), tugma esa uning ichida turadi.
 */
function RowActions({
  item,
  onEdit,
  onAction,
}: {
  item: Vacancy
  onEdit: () => void
  onAction: (kind: VacancyAction) => void
}) {
  const { t } = useLocale()

  return (
    <div className="flex items-center justify-end">
      {item.status === 'Open' && (
        <ActionButton
          icon={PauseCircle}
          label={t('recruiting.vacancyAction.pauseTitle')}
          onClick={() => onAction('pause')}
        />
      )}

      {item.status === 'Paused' && (
        <ActionButton
          icon={Play}
          label={t('recruiting.vacancyAction.resumeTitle')}
          onClick={() => onAction('resume')}
        />
      )}

      {item.status !== 'Closed' && (
        <ActionButton
          icon={CircleSlash}
          label={t('recruiting.vacancyAction.closeTitle')}
          onClick={() => onAction('close')}
        />
      )}

      <ActionButton icon={Pencil} label={t('common.edit')} onClick={onEdit} />

      <ActionButton
        icon={Trash2}
        label={t('common.delete')}
        danger
        onClick={() => onAction('delete')}
      />
    </div>
  )
}

function ActionButton({
  icon: Icon,
  label,
  onClick,
  danger = false,
}: {
  icon: typeof Pencil
  label: string
  onClick: () => void
  danger?: boolean
}) {
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      title={label}
      aria-label={label}
      onClick={(event) => {
        event.stopPropagation()
        onClick()
      }}
    >
      <Icon className={danger ? 'text-[var(--danger-text)]' : undefined} />
    </Button>
  )
}

/** Bo'sh katak chiziqcha bilan — «yuklanmadimi?» degan savol tug'ilmasin. */
function TextCell({ value }: { value: string | null }) {
  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return (
    <span className="truncate-1 text-[var(--fg-secondary)]" title={value}>
      {value}
    </span>
  )
}

function DateCell({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null || value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
