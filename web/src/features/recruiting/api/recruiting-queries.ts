import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useMemo } from 'react'

import { useCatalogList } from '@/features/catalog/api/catalog-queries'
import type { PagedResult } from '@/features/employees/api/employees-api'
import { fileNameFromDisposition, saveBlobAs } from '@/shared/lib/download'
import type { SelectOption } from '@/shared/ui'
import {
  acceptOffer,
  changeVacancyStatus,
  closeCandidate,
  closeOffer,
  deleteCandidate,
  deleteCandidatePhoto,
  deleteResume,
  deleteVacancy,
  fetchCandidate,
  fetchCandidatePhoto,
  fetchCandidates,
  fetchOffers,
  fetchResume,
  fetchStages,
  fetchVacancies,
  recordCandidateStage,
  reassignCandidate,
  registerCandidate,
  saveOffer,
  saveVacancy,
  updateCandidate,
  uploadCandidatePhoto,
  uploadResume,
  type AcceptOfferInput,
  type CandidateFilters,
  type CandidateInput,
  type CandidateListItem,
  type OfferInput,
  type PipelineStage,
  type RegisterCandidateInput,
  type StageOutcome,
  type VacancyFilters,
  type VacancyInput,
} from './recruiting-api'

/*
 * Bo'lim, lavozim va yuridik shaxs ro'yxatlari filtr paneli va
 * vakansiya formasi uchun kerak, lekin ular REKRUTING domeniga tegishli
 * emas — xodimlar moduli ularni allaqachon yig'ib beradi. Qayta
 * so'ramaslik uchun to'g'ridan-to'g'ri o'sha yerdan qayta eksport
 * qilinadi (`employments-queries.ts` dagi bilan aynan bir xil naqsh):
 * kesh kaliti bir xil bo'lgani sababli, HR avval xodimlar ro'yxatini
 * ochgan bo'lsa, bu yerda ikkinchi marta so'rov ketmaydi.
 */
export { useFilterOptions } from '@/features/employees/api/employees-queries'

/*
 * Mas'ul xodimni tanlash ro'yxati.
 *
 * NEGA `features/safety` dan: bu hook AYNAN shu ish uchun o'sha yerda
 * yozilgan (`useEmployees` ustidagi yupqa qatlam, kesh kaliti xodimlar
 * ro'yxatiniki — ya'ni ikkinchi so'rov ketmaydi). Nusxasini bu yerga
 * ko'chirish ikkita bir xil hook hosil qilardi va biri o'zgarganda
 * ikkinchisi jimgina eskirardi.
 *
 * Uning to'g'ri uyi — `features/employees`, lekin o'sha modul bu
 * topshiriq doirasidan tashqarida (unga tegilmaydi). Ko'chirish
 * keyingi ishga qoldirildi; qayta eksport shu qadar aniqki, ko'chirish
 * paytida faqat shu qator o'zgaradi.
 */
export { useEmployeeOptions } from '@/features/safety/api/incidents-queries'

/** Ma'lumotnoma yo'li — bitta joyda, chop etish xatosiga qarshi. */
const VACANCY_TITLES_KIND = 'vacancy-titles' as const

/**
 * Vakansiya nomlari — TANLASH ro'yxati uchun.
 *
 * ⚠️ Bu LAVOZIMLAR ro'yxati EMAS (`useFilterOptions().positions`). Ikkalasi
 * ataylab ajratilgan: e'lon nomi vakansiya ochilganda tanlanadi, lavozim
 * esa taklif qabul qilinganda (`offer-accept-dialog.tsx`). Nega ular bir
 * narsa emasligi backend'dagi `VacancyTitle` domen izohida.
 *
 * NEGA `useCatalogList` to'g'ridan-to'g'ri qayta ishlatiladi: yozuv
 * `/api/catalog/vacancy-titles` da yashaydi va ma'lumotnomalar moduli
 * uni allaqachon o'qiy oladi (`useIncentiveTypes` bilan aynan bir xil
 * naqsh). Nusxa yozish ikkita kesh kaliti hosil qilardi.
 *
 * @param onlyActive `true` — vakansiya OCHISHDA: faolsizlantirilgan
 *   nomga yangi e'lon berilmaydi. `false` — FILTRDA: eski
 *   vakansiyalarning nomi ro'yxatdan yo'qolib qolmasin.
 */
export function useVacancyTitleOptions(onlyActive: boolean): {
  options: SelectOption[]
  isPending: boolean
} {
  const list = useCatalogList(VACANCY_TITLES_KIND, onlyActive)

  const options = useMemo(
    () => (list.data ?? []).map((item) => ({ value: item.id, label: item.name })),
    [list.data],
  )

  return { options, isPending: list.isPending }
}

/**
 * Kesh kalitlari.
 *
 * Barchasi `['recruiting', …]` bilan boshlanadi — `catalog-queries.ts`
 * dagi bilan bir xil naqsh.
 *
 * ⚠️ Mutatsiyalar QO'LDA `invalidateQueries` YOZMAYDI: `providers.tsx`
 * har qanday muvaffaqiyatli yozuv amalidan keyin butun keshni (rasm
 * kalitlaridan tashqari) o'zi bekor qiladi. U yerdagi izohda sabab
 * batafsil: «nima eskirishini sanab chiq» qoidasi doimiy unutilardi.
 *
 * Bitta ISTISNO bor va u o'z joyida asoslangan: `useAcceptOffer`
 * bekor qilishni TAKRORLAYDI, chunki bu amal boshqa modullarning
 * (xodimlar, sinov) keshiga ham tegadi va ro'yxat shu bog'liqlikni
 * hujjatlashtiradi.
 */
export const recruitingKeys = {
  root: ['recruiting'] as const,
  stages: (onlyActive: boolean) => ['recruiting', 'stages', onlyActive] as const,
  vacancies: (filters: VacancyFilters) => ['recruiting', 'vacancies', filters] as const,
  candidates: (filters: CandidateFilters) => ['recruiting', 'candidates', filters] as const,
  candidate: (id: string) => ['recruiting', 'candidate', id] as const,
  offers: (candidateId: string) => ['recruiting', 'offers', candidateId] as const,

  /**
   * Nomzod surati — BINAR kesh va u qolganlaridan ATAYLAB ajratilgan.
   *
   * Kalit ildizi `-photo` bilan tugaydi: `providers.tsx` har muvaffaqiyatli
   * yozuvdan keyin butun keshni bekor qiladi va shu qoidadan chetlab
   * o'tadigan yagona guruh — nomi `-photo` ga tugaydigan kalitlar
   * (`employee-photo`, `user-photo`, `account-photo`). Busiz nomzod
   * kartasida yozilgan har bir bosqich suratni qaytadan yuklatardi.
   */
  photo: (candidateId: string) => ['candidate-photo', candidateId] as const,
}

/* ═══════════════════════════════════════════════════════════ bosqichlar */

/**
 * Voronka bosqichlari.
 *
 * `onlyActive: true` — nomzodni o'tkazish uchun TANLASH ro'yxati
 * (faolsizlantirilganiga o'tkazib bo'lmaydi, server 409 qaytaradi).
 * `false` — ma'lumotnoma sahifasi: u yerda hammasi ko'rinishi kerak,
 * aks holda «yozuv qayerga ketdi?» degan savol tug'ilardi
 * (`catalog-page.tsx` dagi bilan bir xil qaror).
 */
export function useStages(onlyActive: boolean) {
  return useQuery({
    queryKey: recruitingKeys.stages(onlyActive),
    queryFn: ({ signal }) => fetchStages(onlyActive, signal),
  })
}

/* ══════════════════════════════════════════════════════════ vakansiyalar */

export function useVacancies(filters: VacancyFilters) {
  return useQuery({
    queryKey: recruitingKeys.vacancies(filters),
    queryFn: ({ signal }) => fetchVacancies(filters, signal),

    /**
     * Sahifadan sahifaga yoki filtrdan filtrga o'tganda jadval bo'shab
     * qolmaydi — eski ro'yxat ko'rinib turadi, faqat holat
     * «yangilanmoqda» bo'ladi (`useEmploymentEvents` bilan bir xil).
     */
    placeholderData: (previous) => previous,
  })
}

export function useSaveVacancy() {
  return useMutation({
    mutationFn: ({ id, body }: { id: string | null; body: VacancyInput }) => saveVacancy(id, body),
  })
}

export function useChangeVacancyStatus() {
  return useMutation({
    mutationFn: ({
      id,
      action,
      reason,
      closedAt,
    }: {
      id: string
      action: 'close' | 'reopen'
      reason?: string | null
      closedAt?: string | null
    }) => changeVacancyStatus(id, action, { reason, closedAt }),
  })
}

export function useDeleteVacancy() {
  return useMutation({ mutationFn: (id: string) => deleteVacancy(id) })
}

/**
 * Vakansiyalar — TANLASH ro'yxati uchun.
 *
 * NEGA alohida hook va nega `Open` bilan cheklanmagan: nomzodni
 * KIRITISHDA faqat ochiq vakansiya kerak (yopiqqa server nomzod
 * qo'shmaydi — «vakansiya.qabul_qilmaydi»), lekin FILTRDA hammasi
 * kerak — yopilgan vakansiyaning nomzodlari yo'qolib qolmasin.
 * Shu sababli tanlov chaqiruvchida qilinadi, bu yerda emas.
 *
 * `pageSize: 200` — server chegarasi. Kompaniyada bir vaqtda
 * shunchalik ko'p ochiq vakansiya bo'lishi amalda kutilmaydi;
 * bo'lganda esa qidiruv-kombobox kerak bo'ladi va bu alohida ish
 * (`useEmployeeOptions` dagi bilan bir xil ataylab qilingan
 * soddalashtirish).
 */
export function useVacancyOptions(status?: 'Open'): { options: SelectOption[]; isPending: boolean } {
  const list = useVacancies({
    ...(status === undefined ? {} : { status }),
    page: 1,
    pageSize: 200,
    sort: 'title',
  })

  const options = useMemo(
    () =>
      (list.data?.items ?? []).map((item) => ({
        value: item.id,
        /*
          Yorliq — e'lon nomi va ochilgan sana.

          NEGA sana kerak: ilgari yorliqda lavozim VA bo'lim turardi,
          chunki bitta lavozimga ikki bo'limda vakansiya ochilishi odatiy
          hol edi. Vakansiyada bo'lim qolmagach (sababi `Vacancy` domen
          izohida), bir xil nomli ikkita ochiq e'lonni ajratadigan yagona
          narsa — qachon ochilgani. Usiz ro'yxatda ikkita bir xil qator
          turardi va HR qaysi birini tanlaganini bilmasdi.
        */
        label: `${item.title ?? '—'} · ${item.openedAt}`,
      })),
    [list.data],
  )

  return { options, isPending: list.isPending }
}

/* ═════════════════════════════════════════════════════════════ nomzodlar */

export function useCandidates(filters: CandidateFilters, enabled = true) {
  return useQuery({
    queryKey: recruitingKeys.candidates(filters),
    queryFn: ({ signal }) => fetchCandidates(filters, signal),
    enabled,
    placeholderData: (previous) => previous,
  })
}

export function useCandidate(id: string) {
  return useQuery({
    queryKey: recruitingKeys.candidate(id),
    queryFn: ({ signal }) => fetchCandidate(id, signal),
  })
}

export function useRegisterCandidate() {
  return useMutation({ mutationFn: (body: RegisterCandidateInput) => registerCandidate(body) })
}

export function useUpdateCandidate() {
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: CandidateInput }) => updateCandidate(id, body),
  })
}

export function useReassignCandidate() {
  return useMutation({
    mutationFn: ({ id, vacancyId }: { id: string; vacancyId: string }) =>
      reassignCandidate(id, vacancyId),
  })
}

/** ⭐ Voronkaning yagona harakati — bosqich natijasini yozish. */
export function useRecordStage() {
  return useMutation({
    mutationFn: ({
      id,
      ...body
    }: {
      id: string
      stageId: string
      outcome: StageOutcome
      date: string | null
      conclusion: string | null
    }) => recordCandidateStage(id, body),
  })
}

/* ──────────────────────────────────────────────────── voronka taxtasi */

/**
 * Bir ustunda ko'rsatiladigan eng ko'p nomzod.
 *
 * NEGA cheklov bor: ustun sahifalanmaydi (voronkada sahifalash
 * ma'nosiz — ko'z bilan qarash uchun taxta) va cheklovsiz bitta ustun
 * yuzta kartani tortib, brauzerni ham, ko'zni ham toldirardi. Ortiqchasi
 * «yana N ta» qatoriga aylanadi va u JADVAL ko'rinishiga o'tkazadi —
 * u yerda sahifalash bor.
 */
export const FUNNEL_COLUMN_SIZE = 20

/** Voronka filtri: bosqich, sahifa va o'lcham USTUNNING o'zidan keladi. */
export type FunnelFilters = Omit<CandidateFilters, 'stageId' | 'page' | 'pageSize'>

type CandidatePage = PagedResult<CandidateListItem>

/**
 * ⭐ Ustun so'rovining filtri — YAGONA joyda.
 *
 * NEGA bu funksiya bor va nega u aynan shu faylda. Uni ikkita mijoz
 * ishlatadi: ustunning O'ZI (`useFunnelColumn`) va kartani boshqa
 * bosqichga tashlaganda keshni optimistik yamaydigan mutatsiya
 * (`useMoveCandidateToStage`). Ikkinchisi keshni AYNAN birinchisi
 * yozgan kalit bo'yicha topishi shart — bitta maydon (masalan `sort`)
 * ikki joyda boshqacha yozilsa, yamoq boshqa yozuvga tushar va karta
 * ekranda «ko'chmay» qolardi. Bitta manba bunday xatoni umuman
 * imkonsiz qiladi.
 */
function funnelColumnFilters(filters: FunnelFilters, stageId: string): CandidateFilters {
  return {
    ...filters,
    stageId,
    page: 1,
    pageSize: FUNNEL_COLUMN_SIZE,
    /*
      Voronkada tartib DOIM «yangidan eskiga» — bosqichdagi eng yangi
      nomzod tepada. Ro'yxatning umumiy saralashi bu yerga
      ko'chirilmaydi: «bosqich bo'yicha saralash» ustun ichida ma'nosiz
      (hammasi bitta bosqichda), «ism bo'yicha» esa taxtaning
      maqsadiga («kim eng uzoq turibdi?») javob bermaydi.
    */
    sort: '-createdAt',
  }
}

/** Bitta voronka ustuni — nomzodlar ro'yxatining bosqich bo'yicha kesimi. */
export function useFunnelColumn(filters: FunnelFilters, stageId: string) {
  return useCandidates(funnelColumnFilters(filters, stageId))
}

/** Sudralayotgan karta va u chiqib ketayotgan ustun. */
export interface FunnelCard {
  item: CandidateListItem
  fromStageId: string
}

/**
 * Karta ID bo'yicha — qaysi ustunda turganini ham qaytaradi.
 *
 * NEGA kesh o'qiladi, `useDraggable` ning `data` maydoni emas: dnd-kit
 * u yerdagi qiymatni `Record<string, any>` qilib qaytaradi va uni
 * `CandidateListItem` ga aylantirish tekshirilmagan `as` bo'lardi.
 * Kesh esa allaqachon TIPLANGAN va aynan ekranda ko'rinib turgan
 * ma'lumot — ya'ni sudralayotgan kartaning o'zi. Qidiruv faqat
 * ochiq ustunlar bo'yicha yuradi (oltitagacha × 20 yozuv).
 */
export function useFunnelCardFinder(filters: FunnelFilters, stages: PipelineStage[]) {
  const queryClient = useQueryClient()

  return useCallback(
    (candidateId: string): FunnelCard | null => {
      for (const stage of stages) {
        const page = queryClient.getQueryData<CandidatePage>(
          recruitingKeys.candidates(funnelColumnFilters(filters, stage.id)),
        )

        const item = page?.items.find((row) => row.id === candidateId)
        if (item !== undefined) return { item, fromStageId: stage.id }
      }

      return null
    },
    [queryClient, filters, stages],
  )
}

/**
 * ⭐ Kartani boshqa bosqichga ko'chirish — voronkadagi sudrash.
 *
 * <b>So'rov `useRecordStage` bilan AYNAN bir xil endpoint'ga ketadi</b>
 * (`POST /candidates/{id}/stages`), faqat tanasi eng qisqasi:
 * `outcome = Pending`, sana va xulosa bo'sh. Sabab domenda yozilgan —
 * «kutilmoqda» bu «bosqich boshlandi, natija hali yo'q» degani, ya'ni
 * kartani ustunga tashlashning MA'NOSI shu. Sanani server o'zi
 * bugungi qilib qo'yadi (`Date = null` → `_clock.Today`), natijani esa
 * HR keyinroq mavjud oyna orqali yozadi va o'sha yozuv TO'LDIRILADI —
 * tarixda ikkita qator paydo bo'lmaydi.
 *
 * <b>Optimistik yamoq.</b> Karta darhol yangi ustunga ko'chadi: sudrash
 * butun qiymati tezlikda va so'rov javobini kutib turish uni yo'qqa
 * chiqarardi. So'rov yiqilsa ikkala ustun ham suratdan tiklanadi —
 * «yiqildi, lekin ekranda ko'chgan» holati QOLMAYDI. Muvaffaqiyatda
 * hech narsa qilinmaydi: `providers.tsx` butun keshni bekor qiladi va
 * ustunlar serverdan haqiqiy holatni oladi.
 */
export function useMoveCandidateToStage(filters: FunnelFilters) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ card, toStageId }: { card: FunnelCard; toStageId: string; toStageName: string }) =>
      recordCandidateStage(card.item.id, {
        stageId: toStageId,
        outcome: 'Pending',
        date: null,
        conclusion: null,
      }),

    /*
      ⚠️ ATAYLAB `async` EMAS. `cancelQueries` ni kutib turish yamoqni
      keyingi mikrotaskka surardi — o'shanda karta sudrab qo'yilgandan
      keyin bir kadr eski ustunida turib qolardi va sakrab o'tardi.
      Bekor qilish baribir DARHOL boshlanadi (uchayotgan so'rov shu
      yerda uziladi), faqat tugashi kutilmaydi.
    */
    onMutate: ({ card, toStageId, toStageName }) => {
      const fromKey = recruitingKeys.candidates(funnelColumnFilters(filters, card.fromStageId))
      const toKey = recruitingKeys.candidates(funnelColumnFilters(filters, toStageId))

      void queryClient.cancelQueries({ queryKey: fromKey })
      void queryClient.cancelQueries({ queryKey: toKey })

      const restore = [fromKey, toKey].map(
        (key) => [key, queryClient.getQueryData<CandidatePage>(key)] as const,
      )

      queryClient.setQueryData<CandidatePage>(fromKey, (page) =>
        page === undefined ? page : withoutCandidate(page, card.item.id),
      )

      queryClient.setQueryData<CandidatePage>(toKey, (page) =>
        page === undefined
          ? page
          : withCandidate(page, {
              ...card.item,
              currentStageId: toStageId,
              currentStage: toStageName,
            }),
      )

      return { restore }
    },

    onError: (_error, _variables, context) => {
      context?.restore.forEach(([key, page]) => {
        if (page !== undefined) queryClient.setQueryData(key, page)
      })
    },
  })
}

/**
 * Kartani ustundan olib tashlaydi.
 *
 * ⚠️ Bu «klientda filtrlash» EMAS (u bu loyihada taqiqlangan) — bu
 * server javobini bir necha yuz millisekundga oldindan chizish. Haqiqiy
 * ro'yxatni baribir server qaytaradi: muvaffaqiyatdan keyin butun kesh
 * bekor qilinadi va ustun qaytadan yuklanadi.
 */
function withoutCandidate(page: CandidatePage, candidateId: string): CandidatePage {
  const items = page.items.filter((row) => row.id !== candidateId)
  if (items.length === page.items.length) return page

  return { ...page, items, total: Math.max(0, page.total - 1) }
}

/**
 * Kartani ustunga qo'shadi — `-createdAt` tartibini SAQLAB.
 *
 * Ro'yxat kesilmaydi (`FUNNEL_COLUMN_SIZE`): eng eski karta bo'lsa u
 * yigirmanchidan keyinga tushadi va kesish uni ekrandan butunlay
 * yo'qotardi — foydalanuvchi buni «tashlanmadi» deb o'qirdi. Bir zumga
 * 21 ta karta ko'rinishi esa hech narsani buzmaydi.
 */
function withCandidate(page: CandidatePage, item: CandidateListItem): CandidatePage {
  if (page.items.some((row) => row.id === item.id)) return page

  const at = page.items.findIndex((row) => row.createdAt < item.createdAt)
  const items = [...page.items]
  items.splice(at === -1 ? items.length : at, 0, item)

  return { ...page, items, total: page.total + 1 }
}

export function useCloseCandidate() {
  return useMutation({
    mutationFn: ({
      id,
      action,
      at,
      reason,
    }: {
      id: string
      action: 'reject' | 'withdraw'
      at: string | null
      reason: string | null
    }) => closeCandidate(id, action, { at, reason }),
  })
}

export function useDeleteCandidate() {
  return useMutation({ mutationFn: (id: string) => deleteCandidate(id) })
}

/* ─────────────────────────────────────────────────────────────── rezyume */

export function useUploadResume() {
  return useMutation({
    mutationFn: ({ id, file }: { id: string; file: File }) => uploadResume(id, file),
  })
}

export function useDeleteResume() {
  return useMutation({ mutationFn: (id: string) => deleteResume(id) })
}

/**
 * Rezyumeni yuklab olish.
 *
 * NEGA `useQuery` emas, MUTATSIYA: fayl tugma bosilganda kerak bo'ladi,
 * sahifa ochilganda emas — keshda 20 MB baytni saqlab turishning
 * ma'nosi yo'q (`useDownloadDocument` bilan bir xil naqsh).
 *
 * `meta.readOnly` — bu amal serverda hech narsani o'zgartirmaydi, ya'ni
 * undan keyin butun keshni bekor qilish keraksiz (`providers.tsx`
 * dagi qoida shu bayroqni tekshiradi).
 */
export function useDownloadResume() {
  return useMutation({
    meta: { readOnly: true },
    mutationFn: async ({ id, fallbackName }: { id: string; fallbackName: string }) => {
      const { blob, disposition } = await fetchResume(id)
      // Nom SERVERNIKI: u asl fayl nomini biladi. Sarlavha kesilgan
      // bo'lsa (proksi, CORS) — nomzodning ismi bilan saqlanadi.
      saveBlobAs(blob, fileNameFromDisposition(disposition) ?? fallbackName)
    },
  })
}

/* ─────────────────────────────────────────────────────────────── surat */

/**
 * Nomzod surati.
 *
 * ⚠️ Kalitda `photoKey` YO'Q — xodim rasmidan yagona farqi shu.
 * `CandidateDto` saqlash kalitini umuman qaytarmaydi (server qarori: u
 * ichki manzil), ya'ni «yangi kalit — yangi kesh yozuvi» hiylasi bu
 * yerda ishlamaydi. O'rniga kesh QO'LDA yangilanadi: surat almashtirilsa
 * `useUploadCandidatePhoto` shu kalitni bekor qiladi.
 *
 * `hasPhoto` — so'rovning kalitida emas, YOQISH shartida: surati yo'q
 * nomzodda so'rov umuman yuborilmaydi va keraksiz 404 ketmaydi.
 */
export function useCandidatePhoto(candidateId: string, hasPhoto: boolean) {
  return useQuery({
    queryKey: recruitingKeys.photo(candidateId),
    queryFn: ({ signal }) => fetchCandidatePhoto(candidateId, signal),
    enabled: candidateId !== '' && hasPhoto,
    /** Bir nomzod — bir surat. U o'z amalidan tashqari o'zgarmaydi. */
    staleTime: Infinity,
    /**
     * Qayta urinish yo'q: surat chiqmasa bosh harflar qoladi va HR
     * ishida hech narsa to'xtamaydi (`useEmployeePhoto` bilan bir xil).
     */
    retry: false,
  })
}

/**
 * Suratni yuklash.
 *
 * Ikkala bekor qilish ham KERAK: `candidate` — `hasPhoto` bayrog'i
 * uchun, `photo` — baytlarning o'zi uchun. Ikkinchisisiz almashtirilgan
 * surat ekranda eskisi bo'lib qolardi (kalit o'zgarmaydi).
 */
export function useUploadCandidatePhoto(candidateId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file: File) => uploadCandidatePhoto(candidateId, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: recruitingKeys.candidate(candidateId) })
      void queryClient.invalidateQueries({ queryKey: recruitingKeys.photo(candidateId) })
    },
  })
}

/**
 * Suratni o'chirish.
 *
 * Keshdagi blob `invalidate` emas, `remove` qilinadi: `hasPhoto` `false`
 * bo'lgach so'rov o'chadi, qayta yuborishning esa ma'nosi yo'q — u faqat
 * 404 olib kelardi (`useDeleteEmployeePhoto` bilan bir xil qaror).
 */
export function useDeleteCandidatePhoto(candidateId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => deleteCandidatePhoto(candidateId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: recruitingKeys.candidate(candidateId) })
      queryClient.removeQueries({ queryKey: recruitingKeys.photo(candidateId) })
    },
  })
}

/* ═════════════════════════════════════════════════════════════ takliflar */

export function useOffers(candidateId: string) {
  return useQuery({
    queryKey: recruitingKeys.offers(candidateId),
    queryFn: ({ signal }) => fetchOffers(candidateId, signal),
  })
}

export function useSaveOffer(candidateId: string) {
  return useMutation({
    mutationFn: ({ id, body }: { id: string | null; body: OfferInput }) =>
      saveOffer(id, candidateId, body),
  })
}

export function useCloseOffer() {
  return useMutation({
    mutationFn: ({
      id,
      action,
      at,
      note,
    }: {
      id: string
      action: 'decline' | 'expire'
      at: string | null
      note: string | null
    }) => closeOffer(id, action, { at, note }),
  })
}

/**
 * ⚠️ Taklifni QABUL QILISH — nomzod xodimga aylanadi.
 *
 * Yagona joy, qayerda mutatsiya keshni O'ZI ham qo'shimcha bekor
 * qiladi. NEGA `providers.tsx` dagi umumiy qoida yetmaydi: u
 * `onSuccess` dan keyin ishlaydi va faol so'rovlarni yangilaydi —
 * bu yerda esa yaratilgan XODIM boshqa modulning keshiga tegishli
 * (`employees`, `probations`) va HR darhol o'sha kartaga o'tishi
 * mumkin. Umumiy bekor qilish ularni ham qamrab oladi, shu sababli
 * bu yerda faqat NIYAT hujjatlashtiriladi: ro'yxat qaysi keshlar
 * eskirishini aytib turadi.
 */
export function useAcceptOffer() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: AcceptOfferInput }) => acceptOffer(id, body),
    onSuccess: () => {
      // Nomzod, vakansiya (`hiredCount`), xodimlar ro'yxati va sinov
      // muddatlari — hammasi shu bitta amaldan o'zgaradi.
      void queryClient.invalidateQueries({ queryKey: recruitingKeys.root })
      void queryClient.invalidateQueries({ queryKey: ['employees'] })
      void queryClient.invalidateQueries({ queryKey: ['probations'] })
    },
  })
}
