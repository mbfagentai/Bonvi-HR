import { Link, getRouteApi } from '@tanstack/react-router'
import { ArrowRight, Link2, Paperclip, Plus, SearchX, UserPlus, UserRoundX } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import type { SortingState } from '@tanstack/react-table'

import { useAuth } from '@/features/auth'
import { AnketaLinkDialog } from '@/features/kiosk/components/anketa-link-dialog'
import { REPORTS_EXPORT } from '@/features/employees/lib/employee-presentation'
import { useListExport } from '@/features/reports/api/list-export'
import { ListExportError } from '@/features/reports/components/list-export'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { isDateRangeInverted } from '@/shared/lib/list-search'
import { formatPhone } from '@/shared/lib/phone'
import { useClampPage, usePageSize } from '@/shared/lib/use-page-size'
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
  buttonVariants,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import {
  candidateListParams,
  type CandidateFilters,
  type CandidateListFilters,
  type CandidateListItem,
  type PipelineStage,
} from '../api/recruiting-api'
import {
  useCandidates,
  useReferralChannelOptions,
  useStages,
  useVacancyOptions,
  type ReferralChannelOption,
} from '../api/recruiting-queries'
import {
  DEFAULT_SORT,
  filtersToSearch,
  isFiltered,
  normalizeSort,
  referralFilter,
  searchToFilters,
  type CandidatesFilterState,
  type CandidatesSearch,
  type CandidatesView,
} from '../lib/candidates-search'
import { recruitingErrorCode } from '../lib/recruiting-error'
import {
  CANDIDATE_LOOK,
  RECRUITING_MANAGE,
  RECRUITING_VIEW,
  SOURCE_LOOK,
} from '../lib/recruiting-presentation'
import { CandidateFormDialog } from './candidate-form-dialog'
import { CandidateFunnel } from './candidate-funnel'
import { CandidatesToolbar } from './candidates-toolbar'

const helper = dataColumnHelper<CandidateListItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: CandidateListItem[] = []
const NO_STAGES: PipelineStage[] = []
const NO_CHANNELS: ReferralChannelOption[] = []

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
  const canView = hasPermission(RECRUITING_VIEW)
  // Eksport — ro'yxatni ko'rish VA `reports.export` (SPEC-excel-eksport §1)
  const canExport = hasPermission(RECRUITING_VIEW) && hasPermission(REPORTS_EXPORT)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const page = search.page ?? 1
  const sort = search.sort ?? DEFAULT_SORT
  const view: CandidatesView = search.view ?? 'table'
  const filters = useMemo(() => searchToFilters(search), [search])
  const { pageSize, setPageSize, allowAll } = usePageSize('candidates', true)

  /*
    Ro'yxat ramkasi (sarlavha va sahifalash joyida, qatorlar ichida
    aylanadi) — faqat jadvalda. Voronka ustunlari o'z balandligida
    chiziladi va ramkaga qisilsa kesilib qolardi.
  */
  const frame = view === 'table'

  const [creating, setCreating] = useState(false)
  const [anketaLink, setAnketaLink] = useState(false)

  /**
   * ⭐ «RAD ETILGANLAR» BO'LIMI — menyudagi alohida band shu manzilni
   * ochadi (`nav-items.ts`, `?rejected=true`).
   *
   * Alohida SAHIFA emas: bu o'sha ro'yxatning boshqa KESIMI, ya'ni
   * qidiruv, saralash, sahifalash va ustunlar bitta joyda qoladi
   * («ishdan ketganlar» bandidagi bilan bir xil mulohaza). Lekin ekran
   * BO'LIMDEK ko'rinishi kerak — sarlavha, ikona va bo'sh holat matni
   * shu bayroqqa qarab o'zgaradi, aks holda HR «Rad etilganlar» ni
   * bosib, sarlavhasida «Nomzodlar» yozilgan ekranga tushardi va
   * qayerdaligini bilmasdi.
   *
   * ⚠️ Bayroq HOLAT FILTRIDAN olinmaydi (ilgari `status === 'Rejected'`
   * edi). Bo'lim endi IKKITA holatni ko'rsatadi — «Biz rad etdik»
   * (`Rejected`) va «O'zi voz kechdi» (`Withdrawn`) — va ikkalasi ham
   * kundalik ro'yxatdan yashiringan (foydalanuvchi qarori, 2026-09-10:
   * ketgan odam «hozir kim harakatlanyapti?» degan savolga javob emas).
   * Bitta `<select>` esa ikkita qiymatni ifodalay olmaydi, shu sababli
   * bo'lim alohida bayroq bo'ldi (SPEC §4.7).
   *
   * Ikkita holat QO'SHILIB KETMADI: yorliqlar ham (qizil va sariq),
   * voronka hisoboti ham ularni avvalgidek ajratadi — «biz tanlamadik»
   * va «bizni tanlamadi» boshqa-boshqa savollar (`CANDIDATE_LOOK`
   * izohi). Faqat RO'YXATDA KO'RINISHI umumiy bo'ldi. Shu sababli
   * bo'limning nomi ham «Rad etilganlar» bo'lib qoldi: qatordagi
   * yorliqning o'zi kimning ketganini aytib turadi.
   */
  const rejectedSection = search.rejected === true

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

  /*
    «Qayerdan bilgan» filter — EVERY channel, inactive ones too: old
    candidates keep a channel HR has since switched off, and the filter must
    still find them (SPEC-qayerdan-bilgan §6). Inactive rows are marked.
  */
  const referralChannels = useReferralChannelOptions()
  const referralOptions = useMemo(
    () =>
      (referralChannels.data ?? NO_CHANNELS).map((channel) => ({
        value: channel.id,
        label: channel.isActive
          ? channel.name
          : t('recruiting.referralInactive', { name: channel.name }),
      })),
    [referralChannels.data, t],
  )

  /** Jadval va voronka uchun umumiy filtr — bosqich, sahifa va hajmsiz. */
  const sharedFilters = useMemo<Omit<CandidateFilters, 'stageId' | 'page' | 'pageSize'>>(
    () => ({
      ...(filters.search === '' ? {} : { search: filters.search }),
      ...(filters.vacancyId === '' ? {} : { vacancyId: filters.vacancyId }),
      ...(filters.status === '' ? {} : { status: filters.status }),
      ...(filters.source === '' ? {} : { source: filters.source }),
      // Ikkala bayroq faqat YOQILGANDA qo'shiladi — `false` so'rovda
      // hech narsani o'zgartirmagan holda kesh kalitini ikkiga bo'lardi.
      ...(filters.contactDue ? { contactDue: true } : {}),
      ...(rejectedSection ? { rejected: true } : {}),
      /*
        Sana oralig'i jadvalga HAM, voronkaga HAM: ikkalasi bitta ro'yxatning
        ikki chizilishi. Ma'nosini server bo'limga qarab tanlaydi — bu yerda
        ariza kuni, «Rad etilganlar» da rad etilgan kun.
      */
      ...(filters.from === '' ? {} : { from: filters.from }),
      ...(filters.to === '' ? {} : { to: filters.to }),
      // Table, funnel and the Excel file — the same channel filter
      ...referralFilter(filters.referral),
      /*
        ⭐ Qidiruvda rad etilganlar va o'zi voz kechganlar HAM chiqadi
        (egasi, 2026-10-15: «qidirilsa rad etilgan nomzodlardan ham
        qidirsin»). Sukut ro'yxatda ular yashirin, lekin HR odamni ismi
        bilan qidirganda «topilmadi» ni ko'rsa, uni qayta kiritardi.
        Holat tanlangan yoki «Rad etilganlar» bo'limida — kerak emas:
        u yerda ular allaqachon so'rovning o'zida.
      */
      ...(filters.search.trim() !== '' && filters.status === '' && !rejectedSection
        ? { includeRejected: true }
        : {}),
    }),
    [filters, rejectedSection],
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
   *
   * ⚠️ BO'LIM BAYROG'I ham ataylab tanlangan holat hisoblanadi. Ilgari bu
   * tanlov `status=Rejected` bo'lib kelardi va shart o'z-o'zidan
   * ishlardi; endi bo'lim alohida bayroq, ya'ni uni shu yerda TEKSHIRISH
   * SHART. Aks holda «Rad etilganlar» bo'limidagi voronka
   * `rejected=true AND status=InProgress` so'rovini yuborib, HAR SAFAR
   * bo'sh taxta chizardi — bir-birini yo'q qiladigan ikki shart.
   */
  const funnelFilters = useMemo<Omit<CandidateFilters, 'stageId' | 'page' | 'pageSize'>>(
    () => ({
      ...sharedFilters,
      ...(rejectedSection || filters.status !== '' ? {} : { status: 'InProgress' as const }),
    }),
    [sharedFilters, filters.status, rejectedSection],
  )

  /*
    Teskari oraliq («20.10 dan 01.10 gacha») bilan so'rov YUBORILMAYDI —
    na jadval, na voronka: server baribir 400 qaytaradi, ekran esa sababni
    toolbar ostida darhol aytadi (`attendance-passes-page.tsx` naqshi).
  */
  const rangeInvalid = isDateRangeInverted(filters)

  /**
   * Jadval filtri sahifasiz — ro'yxat ham, Excel eksporti ham AYNAN shundan
   * (`employees-page.tsx` dagi `listFilters` naqshi): ekranda nima bo'lsa,
   * faylda ham shu, saralash bilan birga.
   */
  const listFilters = useMemo<CandidateListFilters>(
    () => ({
      ...sharedFilters,
      ...(filters.stageId === '' ? {} : { stageId: filters.stageId }),
      sort,
    }),
    [sharedFilters, filters.stageId, sort],
  )

  const query = useCandidates(
    {
      ...listFilters,
      page,
      pageSize,
    },
    // Voronkada bu so'rov KERAK EMAS: ustunlar o'z so'rovlarini
    // yuboradi va bu yerdagi natija hech qayerda chizilmaydi. Uni
    // shunchaki qoldirish har taxta ochilganda 50 qatorlik ortiqcha
    // so'rov degani.
    view === 'table' && !rangeInvalid,
  )

  /*
    Excelga yuklash. Jadvalda — jadval filtri (bosqich va saralash bilan);
    voronkada — taxtaning o'z filtri: u bosqich tanlovini e'tiborsiz qoldiradi
    va holat tanlanmasa faqat jarayondagilarni ko'rsatadi, fayl ham shunday.
    Xato mutatsiyada qoladi — keyingi bosish uni o'zi tozalaydi
    (`employees-page.tsx` dagi `exportList` izohi).
  */
  const exportList = useListExport()
  const exportCandidates = () =>
    exportList.mutate({
      path: '/api/recruiting/candidates/export',
      params: candidateListParams(view === 'funnel' ? funnelFilters : listFilters),
      slug: rejectedSection ? 'rad_etilgan_nomzodlar' : 'nomzodlar',
    })

  /**
   * ⭐ «Rezervda ham bor» izohi — qidiruv REZERVDAGI nomzodga ham mos
   * kelsa.
   *
   * 2026-09-26 dan rezervdagilar bu ro'yxatda KO'RINMAYDI (egasining
   * qarori) — qidiruvda ham. Natijada HR rezervdagi odamni qidirib
   * «topilmadi» ni ko'rardi va uni QAYTA kiritardi: takroriy yozuv —
   * loyihaning eng qimmat muammosi. Rezervni ro'yxatga qaytarish egasining
   * qaroriga zid, shu sababli jadvalga tegilmaydi: faqat yuqorida qisqa
   * izoh va «Rezerv» bo'limiga havola chiqadi.
   *
   * Ikkinchi so'rov YENGIL — `pageSize: 1`, bizga faqat `total` kerak.
   * `includeArchive` — rezerv sahifasi ham arxivni oladi
   * (`reserve-candidates-page.tsx`), ya'ni izohdagi son havola ochadigan
   * ro'yxat bilan AYNAN bir xil.
   *
   * Qachon so'ralMAYDI — rezervdagilar allaqachon ko'rinib turgan yoki
   * izoh ma'nosiz bo'lgan holatlar:
   *   · holat tanlangan — server holat tanlovida rezervni yashirmaydi,
   *     «Rezervda» tanlansa ular jadvalning o'zida;
   *   · «Bugun aloqa kerak» — bu filtr rezervni o'zi qo'shadi
   *     (`CandidateSearchFilter.HideReserve` izohi);
   *   · manba «Excel arxivi» — arxiv tanlovida ham rezerv ko'rinadi;
   *   · «Rad etilganlar» bo'limi — u boshqa savolga javob beradi;
   *   · voronka — taxta faqat jarayondagilarni chizadi.
   */
  const reserveHintSearch = filters.search.trim()
  const reserveHintEnabled =
    view === 'table' &&
    reserveHintSearch !== '' &&
    filters.status === '' &&
    filters.source !== 'Excel' &&
    !filters.contactDue &&
    !rejectedSection

  const reserveMatches = useCandidates(
    {
      search: reserveHintSearch,
      status: 'Reserve',
      includeArchive: true,
      page: 1,
      pageSize: 1,
    },
    reserveHintEnabled,
  )

  // Oldingi qidiruvning soni yangi qidiruv ostida bir zum ham turmasin —
  // `useCandidates` sahifalash uchun eski javobni ushlab turadi
  const reserveCount =
    reserveHintEnabled && reserveMatches.isSuccess && !reserveMatches.isPlaceholderData
      ? reserveMatches.data.total
      : 0

  /*
   * ⭐ Qora ro'yxat izohi — rezervdagining egizagi (2026-10-07). Qora
   * ro'yxatdagi nomzod HECH QAYSI ro'yxatda ko'rinmaydi
   * (`CandidateSearchFilter.HideBlacklisted`), ya'ni HR ismini qidirib
   * topmasa, uni yangidan kiritib yuborishi mumkin — aynan qora ro'yxat
   * oldini olishi kerak bo'lgan holat. Shu sababli izoh «Rad etilganlar»
   * bo'limida va arxiv tanlovida HAM chiqadi (u yerda ham ular yo'q).
   */
  const blacklistHintEnabled =
    view === 'table' && reserveHintSearch !== '' && filters.status === '' && !filters.contactDue

  const blacklistMatches = useCandidates(
    {
      search: reserveHintSearch,
      status: 'Blacklisted',
      includeArchive: true,
      page: 1,
      pageSize: 1,
    },
    blacklistHintEnabled,
  )

  const blacklistCount =
    blacklistHintEnabled && blacklistMatches.isSuccess && !blacklistMatches.isPlaceholderData
      ? blacklistMatches.data.total
      : 0

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

  /** Hajm o'zgarsa birinchi sahifadan — eski raqam yangi hajmda boshqa odamlar. */
  const changePageSize = useCallback(
    (next: number) => {
      setPageSize(next)
      setSearch((previous) => ({ ...previous, page: undefined }))
    },
    [setPageSize, setSearch],
  )

  // Eski sahifa raqami yangi hajmda bo'sh bo'lsa — oxirgi sahifaga (`useClampPage`)
  const clampPage = useCallback(
    (last: number) =>
      setSearch((previous) => ({ ...previous, page: last <= 1 ? undefined : last })),
    [setSearch],
  )
  useClampPage(page, pageSize, query.isPlaceholderData ? undefined : query.data?.total, clampPage)

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
        /*
          YAGONA `minWidth`li ustun — ortgan joyni F.I.Sh. oladi. Ism
          kesilmaydi va qatorga bo'linmaydi (egasi 2026-10-22: har yozuv bir
          qatorda) — ustun eng uzun ismgacha kengayadi.
        */
        meta: { minWidth: '260px' },
        cell: ({ row }) => (
          <div className="flex items-center gap-2">
            <span className="font-medium">{row.original.fullName}</span>

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
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('currentStage', {
        id: 'stage',
        header: t('recruiting.stage'),
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      /*
        Keyingi aloqa sanasi uchun USTUN YO'Q — foydalanuvchi qarori
        (2026-09-10): jadval allaqachon keng va sana har kuni emas,
        kerak bo'lganda qaraladi. Ma'lumotning o'zi yo'qolmadi — u
        nomzod kartasida turadi, «Bugun aloqa kerak» filtri esa
        ro'yxatni aynan shu belgiga qarab toraytiradi.

        Ustun bilan birga saralash ham ketdi (u ustun sarlavhasidan
        boshqarilardi). Server `sort=nextContact` ni hamon tushunadi —
        shartnoma torayтirilmadi, chunki ustun qaytarilishi mumkin.
      */

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
        // Manba ro'yxatda ATAYLAB ko'rinib turadi — keyingi bosqichda
        // Telegram boti ulanadi va farq darhol sezilishi kerak
        // (`SOURCE_LOOK` izohi). Kenglik mazmundan.
        meta: { hideOnMobile: true },
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
        // 170px — formatlangan raqam («+998 90 123 45 67», 17 belgi)
        meta: { width: '170px', hideOnMobile: true },
        cell: ({ getValue }) => {
          const value = getValue()
          if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
          return (
            <span className="tabular whitespace-nowrap text-[var(--fg-secondary)]">
              {formatPhone(value)}
            </span>
          )
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

  // Teskari oraliqda eski sahifa ko'rinib qolmasin — u boshqa filtrniki
  const rows = rangeInvalid ? EMPTY : (query.data?.items ?? EMPTY)
  const total = rangeInvalid ? 0 : (query.data?.total ?? 0)
  const firstLoad = query.isPending && view === 'table' && !rangeInvalid
  const filtered = isFiltered(search)
  const listEmpty = rows.length === 0 && !firstLoad

  /**
   * Bo'limning O'ZIDAN boshqa filtr qo'yilmagan — ro'yxat haqiqatan
   * bo'sh, «topilmadi» emas.
   *
   * Bo'sh «Rad etilganlar» bo'limi «qidiruv bo'yicha hech narsa
   * topilmadi» deb yozmasligi kerak — HR hech narsa qidirmagan.
   *
   * Ilgari bu yerda `isFiltered({ ...search, status: undefined })`
   * turardi: bo'lim holat filtri bo'lgani uchun uni qo'lda o'chirib
   * tashlash kerak edi. Endi bayroq alohida va `isFiltered` uni O'ZI
   * sanamaydi (`candidates-search.ts` izohi), ya'ni shart oddiy so'rovga
   * aylandi.
   */
  const emptySection = rejectedSection && !isFiltered(search)

  const viewOptions = [
    { value: 'table' as const, label: t('recruiting.viewTable') },
    { value: 'funnel' as const, label: t('recruiting.viewFunnel') },
  ]

  return (
    <div
      data-list-frame={frame ? '' : undefined}
      // Voronka ham ekranning butun eni — ustunlar ko'proq sig'adi (`app-shell.tsx`)
      data-wide-list={frame ? undefined : ''}
      className={cn('space-y-6', frame && 'frame:flex frame:h-full frame:flex-col')}
    >
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            {/*
              Ohang `red` EMAS, `gray`: bu ARXIV, xato emas. Qizil
              ekranni «bu yerda nimadir buzilgan» deb o'qitardi.
            */}
            <IconTile
              icon={rejectedSection ? UserRoundX : UserPlus}
              tone={rejectedSection ? 'gray' : 'brand'}
              size="lg"
            />
            {t(rejectedSection ? 'nav.rejectedCandidates' : 'nav.candidates')}
          </span>
        }
        // Voronkada son yo'q (ro'yxat bosqichlar bo'yicha yuklanadi) — panelda faqat sarlavha
        subtitle={
          view === 'funnel'
            ? undefined
            : firstLoad
              ? t('common.loading')
              : t('recruiting.candidateSubtitle', { n: num(total) })
        }
        description={view === 'funnel' ? t('recruiting.funnelSubtitle') : undefined}
        // Ko'rinish almashtirgichi — sahifada, avvalgi joyida (egasi 2026-10-22)
        toolbar={
          <SegmentedControl
            value={view}
            onChange={changeView}
            options={viewOptions}
            size="sm"
            aria-label={t('recruiting.view')}
          />
        }
        actions={
          /*
            «Nomzod qo'shish» rad etilganlar bo'limida ATAYLAB yo'q:
            yangi nomzod har doim `InProgress` bo'lib tug'iladi, ya'ni
            u shu ro'yxatga tushmasdi — HR qo'shgan odam ko'z oldida
            yo'q bo'lib qolardi.
          */
          (canView || canManage) && !rejectedSection ? (
            <>
              {/* The public HR-F02 form link (`docs/SPEC-planshet-anketa.md` §8) */}
              {canView && (
                // Icon only on a phone: the header row already carries the view switch and «+».
                <Button
                  variant="secondary"
                  pill
                  onClick={() => setAnketaLink(true)}
                  title={t('kiosk.link.button')}
                  className="max-sm:size-10 max-sm:px-0"
                >
                  <Link2 />
                  <span className="max-sm:sr-only">{t('kiosk.link.button')}</span>
                </Button>
              )}
              {canManage && (
                <Button variant="create" onClick={() => setCreating(true)}>
                  <Plus />
                  {t('recruiting.candidateCreate')}
                </Button>
              )}
            </>
          ) : undefined
        }
      />

      {query.isError && (
        <Alert tone="danger">{errorMessage(recruitingErrorCode(query.error))}</Alert>
      )}

      {/*
        Asosiy ro'yxat bo'sh bo'lsa ham, to'la bo'lsa ham chiqadi: HR
        qidirgan odam jadvaldagi boshqa qatorlar orasida bo'lmasligi va
        aynan rezervda turgan bo'lishi mumkin. Izoh yiqilgan so'rovda
        jim qoladi — bu yordamchi ma'lumot, asosiy ro'yxatning xatosi
        yuqorida allaqachon ko'rinadi.
      */}
      {reserveCount > 0 && (
        <Alert tone="info">
          <span className="flex flex-wrap items-center gap-2">
            <span>{t('recruiting.reserveSearchHint', { n: num(reserveCount) })}</span>

            <Link
              to="/rezerv"
              search={{ q: reserveHintSearch }}
              className={buttonVariants({ variant: 'ghost', size: 'sm' })}
            >
              {t('recruiting.reserveSearchOpen')}
              <ArrowRight />
            </Link>
          </span>
        </Alert>
      )}

      {blacklistCount > 0 && (
        <Alert tone="danger">
          <span className="flex flex-wrap items-center gap-2">
            <span>{t('recruiting.blacklistSearchHint', { n: num(blacklistCount) })}</span>

            <Link
              to="/qora-royxat"
              search={{ q: reserveHintSearch }}
              className={buttonVariants({ variant: 'ghost', size: 'sm' })}
            >
              {t('recruiting.reserveSearchOpen')}
              <ArrowRight />
            </Link>
          </span>
        </Alert>
      )}

      <Card className={cn('overflow-hidden', frame && 'frame:flex frame:flex-1 frame:flex-col')}>
        <CandidatesToolbar
          filters={filters}
          onChange={changeFilters}
          vacancies={allVacancies.options}
          stages={stageOptions}
          referralChannels={referralOptions}
          rejectedSection={rejectedSection}
          onExport={canExport ? exportCandidates : undefined}
          exporting={exportList.isPending}
          exportDisabled={rangeInvalid}
        />

        {rangeInvalid && (
          <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
            <Alert tone="warning">{errorMessage('royxat.sana_oraligi_notogri')}</Alert>
          </div>
        )}

        {/* Eksport xatosi — tugmaning ostida, kartaning ichida (xodimlar ro'yxatidagi joy) */}
        <ListExportError error={exportList.error} />

        {view === 'funnel' ? (
          rangeInvalid ? null : (
            <CandidateFunnel
              stages={activeStages.data ?? NO_STAGES}
              stagesLoading={activeStages.isPending}
              filters={funnelFilters}
              onOpen={openCandidate}
              onShowAll={showStageInTable}
            />
          )
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
              fill
              empty={
                // Teskari oraliqda bo'sh holat CHIZILMAYDI — sabab yuqoridagi
                // ogohlantirishda, «topilmadi» esa yolg'on bo'lardi
                listEmpty && !rangeInvalid ? (
                  emptySection ? (
                    <EmptyState
                      icon={UserRoundX}
                      title={t('recruiting.candidateNoRejectedTitle')}
                      description={t('recruiting.candidateNoRejectedText')}
                    />
                  ) : filtered ? (
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
                pageSize={pageSize}
                total={total}
                onPageChange={changePage}
                onPageSizeChange={changePageSize}
                allowAll={allowAll}
                disabled={query.isFetching}
                className="border-t border-[var(--border)]"
              />
            )}
          </>
        )}
      </Card>

      {canView && <AnketaLinkDialog open={anketaLink} onClose={() => setAnketaLink(false)} />}

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
  return <span className="text-[var(--fg-secondary)]">{value}</span>
}

/**
 * Sana katagi.
 *
 * ⚠️ Tipi `string | null`, ikkalasi ham chiziqchaga aylanadi. Bo'sh SATR
 * `createdAt` dan keladi (o'qishda `?? ''` zaxirasi bor), `null` esa
 * haqiqiy «kiritilmagan» qiymat — `nextContactDate` odatda aynan shunday.
 * Ilgari tip faqat `string` edi va `null` uzatilsa `new Date(null)`
 * 1970-yilni qaytarib, katakda «01.01.1970» turardi — chiziqcha emas.
 */
function DateCell({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null || value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
