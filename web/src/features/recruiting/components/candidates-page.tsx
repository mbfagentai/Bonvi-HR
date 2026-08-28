import { getRouteApi } from '@tanstack/react-router'
import { Paperclip, Plus, SearchX, UserPlus } from 'lucide-react'
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
  SegmentedControl,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import type { CandidateFilters, CandidateListItem, PipelineStage } from '../api/recruiting-api'
import { useCandidates, useStages, useVacancyOptions } from '../api/recruiting-queries'
import {
  DEFAULT_SORT,
  filtersToSearch,
  isFiltered,
  normalizeSort,
  searchToFilters,
  type CandidatesFilterState,
  type CandidatesSearch,
  type CandidatesView,
} from '../lib/candidates-search'
import { recruitingErrorCode } from '../lib/recruiting-error'
import {
  CANDIDATE_LOOK,
  RECRUITING_MANAGE,
  SOURCE_LOOK,
} from '../lib/recruiting-presentation'
import { CandidateFormDialog } from './candidate-form-dialog'
import { CandidateFunnel } from './candidate-funnel'
import { CandidatesToolbar } from './candidates-toolbar'

const helper = dataColumnHelper<CandidateListItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: CandidateListItem[] = []
const NO_STAGES: PipelineStage[] = []

/** Xodimlar va buyruqlar ro'yxati bilan bir xil (50). */
const PAGE_SIZE = 50

const route = getRouteApi('/_app/nomzodlar/')

/**
 * ⭐ Nomzodlar — 2-bosqichning ASOSIY ekrani.
 *
 * <b>Ikkita ko'rinish, BITTA ma'lumot.</b> Jadval «hammasini ro'yxat
 * qilib ber» degan savolga javob beradi (saralash, sahifalash,
 * qidiruv), voronka esa «kim qaysi bosqichda turibdi» degan savolga.
 * Ikkalasi ham AYNAN bir xil so'rovni ishlatadi, faqat boshqacha
 * filtr bilan — voronka alohida endpoint EMAS (`candidate-funnel.tsx`
 * boshidagi izoh).
 *
 * Ko'rinish manzil satrida (`?view=funnel`): HR taxtani ochib, nomzod
 * kartasiga kirib, «orqaga» bosganda jadvalga tushib qolmasligi kerak.
 *
 * <b>Jadval STANDART, voronka emas.</b> Voronka chiroyliroq, lekin
 * jadval ko'proq savolga javob beradi: qidiruv, saralash va
 * sahifalash faqat unda ishlaydi. Sukut bo'yicha eng ko'p ishlatiladigan
 * ko'rinish turishi kerak, eng ko'rkam emas.
 */
export function CandidatesPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(RECRUITING_MANAGE)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const page = search.page ?? 1
  const sort = search.sort ?? DEFAULT_SORT
  const view: CandidatesView = search.view ?? 'table'
  const filters = useMemo(() => searchToFilters(search), [search])

  const [creating, setCreating] = useState(false)

  /*
   * Filtr ro'yxatlari. Vakansiyalar — HAMMASI (yopilganini ham):
   * yopilgan vakansiyaning nomzodlari yo'qolib qolmasligi kerak.
   * Nomzod KIRITISHDA esa faqat ochiqlari kerak, shu sababli ikkinchi
   * ro'yxat alohida so'raladi — ikkalasining kesh kaliti boshqa-boshqa,
   * bir-birini bekor qilmaydi.
   */
  const allVacancies = useVacancyOptions()
  const openVacancies = useVacancyOptions('Open')

  /*
   * Bosqichlar: filtr uchun HAMMASI, voronka ustunlari uchun faqat
   * FAOLLARI. Bitta so'rovdan ikkisini yasab bo'lmaydi — server
   * `onlyActive` ni o'zi qo'llaydi va ikkita kesh kaliti bor.
   * Filtr ro'yxatida faolsizlantirilgan bosqich ham turishi SHART:
   * unda qolib ketgan nomzodlarni topishning boshqa yo'li yo'q.
   */
  const allStages = useStages(false)
  const activeStages = useStages(true)

  /** FILTR ro'yxati — hammasi, faolsizlantirilgani bilan birga. */
  const stageOptions = useMemo(
    () => (allStages.data ?? NO_STAGES).map((stage) => ({ value: stage.id, label: stage.name })),
    [allStages.data],
  )

  /**
   * TANLASH ro'yxati — faqat faollari.
   *
   * NEGA filtrnikidan alohida: nomzodni faolsizlantirilgan bosqichga
   * o'tkazib bo'lmaydi va server buni 409 bilan rad etadi
   * («bosqich.faol_emas»). Bitta ro'yxatni ikkala joyda ishlatsak, HR
   * ko'rinib turgan qatorni tanlab, keyin xato olardi.
   */
  const activeStageOptions = useMemo(
    () => (activeStages.data ?? NO_STAGES).map((stage) => ({ value: stage.id, label: stage.name })),
    [activeStages.data],
  )

  /** Jadval va voronka uchun umumiy filtr — bosqich, sahifa va hajmsiz. */
  const sharedFilters = useMemo<Omit<CandidateFilters, 'stageId' | 'page' | 'pageSize'>>(
    () => ({
      ...(filters.search === '' ? {} : { search: filters.search }),
      ...(filters.vacancyId === '' ? {} : { vacancyId: filters.vacancyId }),
      ...(filters.status === '' ? {} : { status: filters.status }),
      ...(filters.source === '' ? {} : { source: filters.source }),
    }),
    [filters],
  )

  /**
   * Voronka filtri — jadvalnikidan BITTA farq bilan.
   *
   * Holat tanlanmagan bo'lsa, taxta faqat JARAYONDAGI nomzodlarni
   * ko'rsatadi (`status=InProgress` — backend hujjatidagi voronka
   * ustunining o'zi). Sabab: rad etilgan va ishga olingan nomzodlar
   * oxirgi bosqichlarida qotib qoladi va yillar davomida to'planadi —
   * ular ustunlarni to'ldirib, «hozir kim harakatlanyapti?» degan
   * savolni ko'mib yuborardi.
   *
   * Ataylab tanlangan holat esa HURMAT QILINADI: HR «rad etilganlar»ni
   * tanlab, ular qaysi bosqichda to'xtaganini ko'ra oladi — voronkaning
   * eng foydali savollaridan biri aynan shu.
   */
  const funnelFilters = useMemo<Omit<CandidateFilters, 'stageId' | 'page' | 'pageSize'>>(
    () => ({ ...sharedFilters, status: filters.status === '' ? 'InProgress' : filters.status }),
    [sharedFilters, filters.status],
  )

  const query = useCandidates(
    {
      ...sharedFilters,
      ...(filters.stageId === '' ? {} : { stageId: filters.stageId }),
      page,
      pageSize: PAGE_SIZE,
      sort,
    },
    // Voronkada bu so'rov KERAK EMAS: ustunlar o'z so'rovlarini
    // yuboradi va bu yerdagi natija hech qayerda chizilmaydi. Uni
    // shunchaki qoldirish har taxta ochilganda 50 qatorlik ortiqcha
    // so'rov degani.
    view === 'table',
  )

  const setSearch = useCallback(
    (
      next: (previous: CandidatesSearch) => CandidatesSearch,
      history: 'push' | 'replace' = 'replace',
    ) => {
      void navigate({ to: '/nomzodlar', search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  const changeFilters = useCallback(
    (next: CandidatesFilterState) => {
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

  const changeView = useCallback(
    (next: CandidatesView) => {
      // Ko'rinish o'zgarganda sahifa tushiriladi: voronkada sahifa
      // yo'q va u yerdan qaytganda 7-sahifada qolib ketish g'alati.
      setSearch((previous) => ({
        ...previous,
        view: next === 'table' ? undefined : next,
        page: undefined,
      }))
    },
    [setSearch],
  )

  /** SERVER tomonda saralash — sabab `vacancies-page.tsx` da bir xil. */
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

  const openCandidate = useCallback(
    (candidate: CandidateListItem) => {
      void navigate({ to: '/nomzodlar/$candidateId', params: { candidateId: candidate.id } })
    },
    [navigate],
  )

  /** Voronka ustunidagi «yana N ta» — o'sha bosqich bilan jadvalga o'tadi. */
  const showStageInTable = useCallback(
    (stage: PipelineStage) => {
      setSearch(
        (previous) => ({ ...previous, stage: stage.id, view: undefined, page: undefined }),
        'push',
      )
    },
    [setSearch],
  )

  const columns = useMemo<Array<DataColumn<CandidateListItem>>>(
    () => [
      helper.accessor('fullName', {
        // Server `name` kalitini tanidi, ustun esa `fullName` deb
        // ataladi — saralash uchun `id` ATAYLAB almashtiriladi
        // (`normalizeSort` faqat serverning uchta kalitini qabul qiladi).
        id: 'name',
        header: t('employee.fullName'),
        /* YAGONA `minWidth`li ustun — ortgan joyni F.I.Sh. oladi. */
        meta: { minWidth: '260px' },
        cell: ({ row }) => (
          <div className="flex min-w-0 items-center gap-2">
            <p className="truncate-1 font-medium">{row.original.fullName}</p>

            {/*
              Rezyume bor-yo'qligi — kichik qisqich ikonasi. Alohida
              ustun ATAYLAB emas: u faqat «bor/yo'q» degan bitta bit
              va butun ustunni egallashga arzimaydi.
            */}
            {row.original.hasResume && (
              <Paperclip
                className="size-3.5 shrink-0 text-[var(--fg-tertiary)]"
                strokeWidth={2}
                aria-label={t('recruiting.hasResume')}
              />
            )}
          </div>
        ),
      }),

      helper.accessor('vacancy', {
        header: t('recruiting.vacancy'),
        enableSorting: false,
        meta: { width: '200px' },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('currentStage', {
        id: 'stage',
        header: t('recruiting.stage'),
        meta: { width: '170px' },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('status', {
        header: t('recruiting.status'),
        enableSorting: false,
        // 150px — eng uzun tarjima «O‘zi voz kechgan» / «Отказался сам».
        meta: { width: '150px' },
        cell: ({ row }) => {
          const look = CANDIDATE_LOOK[row.original.status]
          return (
            <Badge tone={look.tone} size="sm">
              {t(`recruiting.candidateStatus.${look.key}`)}
            </Badge>
          )
        },
      }),

      helper.accessor('source', {
        header: t('recruiting.sourceLabel'),
        enableSorting: false,
        // 130px. Manba ro'yxatda ATAYLAB ko'rinib turadi — keyingi
        // bosqichda Telegram boti ulanadi va farq darhol sezilishi
        // kerak (`SOURCE_LOOK` izohi).
        meta: { width: '130px', hideOnMobile: true },
        cell: ({ row }) => {
          const look = SOURCE_LOOK[row.original.source]
          const Icon = look.icon
          return (
            <span className="flex items-center gap-1.5 text-caption1 text-[var(--fg-secondary)]">
              <Icon className="size-3.5 shrink-0" strokeWidth={2} />
              {t(`recruiting.source.${look.key}`)}
            </span>
          )
        },
      }),

      helper.accessor('phone', {
        header: t('employee.phone'),
        enableSorting: false,
        meta: { width: '150px', hideOnMobile: true },
        cell: ({ getValue }) => {
          const value = getValue()
          if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
          return <span className="tabular text-[var(--fg-secondary)]">{value}</span>
        },
      }),

      helper.accessor('createdAt', {
        header: t('recruiting.createdAt'),
        meta: { width: '130px' },
        cell: ({ getValue }) => <DateCell value={getValue()} />,
      }),
    ],
    [t],
  )

  const rows = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0
  const firstLoad = query.isPending && view === 'table'
  const filtered = isFiltered(search)
  const listEmpty = rows.length === 0 && !firstLoad

  const viewOptions = [
    { value: 'table' as const, label: t('recruiting.viewTable') },
    { value: 'funnel' as const, label: t('recruiting.viewFunnel') },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={UserPlus} tone="brand" size="lg" />
            {t('nav.candidates')}
          </span>
        }
        subtitle={
          view === 'funnel'
            ? t('recruiting.funnelSubtitle')
            : firstLoad
              ? t('common.loading')
              : t('recruiting.candidateSubtitle', { n: num(total) })
        }
        actions={
          <div className="flex flex-wrap items-center gap-3">
            <SegmentedControl
              value={view}
              onChange={changeView}
              options={viewOptions}
              size="sm"
              aria-label={t('recruiting.view')}
            />

            {canManage && (
              <Button variant="create" onClick={() => setCreating(true)}>
                <Plus />
                {t('recruiting.candidateCreate')}
              </Button>
            )}
          </div>
        }
      />

      {query.isError && (
        <Alert tone="danger">{errorMessage(recruitingErrorCode(query.error))}</Alert>
      )}

      <Card className="overflow-hidden">
        <CandidatesToolbar
          filters={filters}
          onChange={changeFilters}
          vacancies={allVacancies.options}
          stages={stageOptions}
        />

        {view === 'funnel' ? (
          <CandidateFunnel
            stages={activeStages.data ?? NO_STAGES}
            stagesLoading={activeStages.isPending}
            filters={funnelFilters}
            onOpen={openCandidate}
            onShowAll={showStageInTable}
          />
        ) : (
          <>
            <DataTable
              columns={columns}
              data={rows}
              getRowId={(item) => item.id}
              loading={firstLoad}
              skeletonRows={8}
              onRowClick={openCandidate}
              rowLabel={(item) => item.fullName}
              sorting={sorting}
              onSortingChange={changeSorting}
              empty={
                listEmpty ? (
                  filtered ? (
                    <EmptyState
                      icon={SearchX}
                      title={t('recruiting.candidateNotFoundTitle')}
                      description={t('recruiting.candidateNotFoundText')}
                    />
                  ) : (
                    <EmptyState
                      icon={UserPlus}
                      title={t('recruiting.candidateEmptyTitle')}
                      description={t('recruiting.candidateEmptyText')}
                      action={
                        canManage ? (
                          <Button variant="create" onClick={() => setCreating(true)}>
                            <Plus />
                            {t('recruiting.candidateCreate')}
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
          </>
        )}
      </Card>

      <CandidateFormDialog
        open={creating}
        item={null}
        vacancyOptions={openVacancies.options}
        stageOptions={activeStageOptions}
        /*
          Vakansiya filtri qo'yilgan bo'lsa, yangi nomzod O'SHA o'ringa
          kiritiladi: HR aynan shu ro'yxatga qarab turib «qo'shish»
          bosgan — vakansiyani qaytadan tanlashi ortiqcha qadam.
        */
        defaultVacancyId={filters.vacancyId}
        onClose={() => setCreating(false)}
      />
    </div>
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

function DateCell({ value }: { value: string }) {
  const { date } = useLocale()

  if (value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
