import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'

import { useCatalogList } from '@/features/catalog/api/catalog-queries'
import { fileNameFromDisposition, saveBlobAs } from '@/shared/lib/download'
import type { SelectOption } from '@/shared/ui'
import {
  acceptOffer,
  changeVacancyStatus,
  closeCandidate,
  closeOffer,
  deleteCandidate,
  deleteResume,
  deleteVacancy,
  fetchCandidate,
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
  uploadResume,
  type AcceptOfferInput,
  type CandidateFilters,
  type CandidateInput,
  type OfferInput,
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
