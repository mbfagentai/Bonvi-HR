import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useMemo } from 'react'

import type { PagedResult } from '@/features/employees/api/employees-api'
import { fileNameFromDisposition, saveBlobAs } from '@/shared/lib/download'
import { UZ_PHONE_LENGTH, uzPhoneDigits } from '@/shared/lib/phone'
import type { SelectOption } from '@/shared/ui'
import {
  changeVacancyStatus,
  closeCandidate,
  deleteCandidateFile,
  deleteCandidatePhoto,
  deleteVacancy,
  fetchCandidate,
  fetchCandidateFile,
  fetchCandidatePhoneMatches,
  fetchCandidatePhoto,
  fetchCandidates,
  fetchStages,
  fetchVacancies,
  hireCandidate,
  inviteCandidateToInterview,
  editCandidateStageEntry,
  recordCandidateStage,
  reassignCandidate,
  registerCandidate,
  returnCandidateToFunnel,
  saveVacancy,
  updateCandidate,
  uploadCandidateFile,
  uploadCandidatePhoto,
  type CandidateFilters,
  type CandidateInput,
  type CandidateListItem,
  type HireCandidateInput,
  type PipelineStage,
  type RegisterCandidateInput,
  type Vacancy,
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
 * Xodimni tanlash ro'yxati — ishga olishda biriktiriladigan xodim
 * (`candidate-hire-dialog.tsx`).
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
 * Bitta ISTISNO bor va u o'z joyida asoslangan: `useHireCandidate`
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
  phoneMatches: (phone: string, exceptId: string | null) =>
    ['recruiting', 'phone-matches', phone, exceptId] as const,

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

  /**
   * Biriktirilgan RASMNI katta ko'rish uchun binar kesh.
   *
   * ⚠️ Kalit ildizi `-photo` bilan tugashi SHART (`candidate-file-photo`)
   * — yuqoridagi bilan aynan bir xil sabab: `providers.tsx` har
   * muvaffaqiyatli yozuvdan keyin butun keshni bekor qiladi va bu
   * qoidadan chetlab o'tadigan yagona guruh — nomi `-photo` ga
   * tugaydigan kalitlar. Suffikssiz nomzod kartasida yozilgan HAR BIR
   * izoh ochiq turgan rasmni qaytadan yuklatardi (bir necha megabayt).
   *
   * Kalitda `fileId` bor: bir nomzodda o'ntagacha fayl bo'ladi va
   * ularning har biri o'z yozuviga tushishi kerak.
   */
  fileBlob: (candidateId: string, fileId: string) =>
    ['candidate-file-photo', candidateId, fileId] as const,
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

/**
 * Yopish / qayta ochish — ro'yxatdan BIR BOSISHDA.
 *
 * <b>Javob keshga DARHOL yoziladi.</b> Umumiy qoida (`providers.tsx`)
 * keshni bekor qiladi, lekin qayta yuklashni KUTMAYDI: tugmadagi aylana
 * to'xtagach, ro'yxat yangilanguncha qatorda ESKI holat bir zum qolardi
 * va HR «bosilmadi» deb ikkinchi marta bosardi — endi esa server
 * «vakansiya.allaqachon_yopiq» deb rad etardi. Server yangilangan
 * vakansiyani qaytaradi, shu sababli uni joyiga qo'yish yetarli; qayta
 * yuklash baribir fonda o'tadi va filtrga tushmay qolgan qatorni
 * olib tashlaydi.
 */
export function useChangeVacancyStatus() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: 'close' | 'reopen' }) =>
      changeVacancyStatus(id, action),
    onSuccess: (updated) => {
      client.setQueriesData<PagedResult<Vacancy>>(
        { queryKey: [...recruitingKeys.root, 'vacancies'] },
        (page) =>
          page === undefined
            ? page
            : {
                ...page,
                items: page.items.map((item) => (item.id === updated.id ? updated : item)),
              },
      )
    },
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

          Server endi YANGI takrorga yo'l qo'ymaydi («vakansiya.nom_band»),
          lekin ilgari ochilgan bir xil nomli vakansiyalar bazada qoladi —
          sana shu sababli ham kerak.
        */
        label: `${item.title} · ${item.openedAt}`,
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

/**
 * ⭐ Shu telefon raqamli boshqa nomzodlar — forma ostidagi OGOHLANTIRISH.
 *
 * <b>So'rov faqat TO'LIQ raqamda ketadi</b> (9 xona). Kalit raqamning
 * o'zi, ya'ni HR har harf terganda so'rov yubormaydi: 1–8 xonali qiymatda
 * hook o'chiq turadi, to'qqizinchi xona bosilganda BITTA so'rov ketadi.
 * Alohida kechiktirish (debounce) shu sababli kerak emas — qisman raqam
 * serverga umuman yetib bormaydi.
 *
 * `placeholderData` ATAYLAB yo'q: raqam o'zgarganda eski raqamning
 * mosliklari yangisining ostida bir zum ham turmasligi kerak — ular
 * boshqa odamlar haqida yolg'on ogohlantirish bo'lardi.
 *
 * @param phone Formadagi qiymat — 9 xona, bo'shliqsiz (`PhoneInput`).
 * @param exceptId Tahrirlanayotgan nomzod; yangi nomzodda `null`.
 * @param enabled Oyna ochiqmi. Forma komponenti yopiq holda ham o'rnatilgan
 *   turadi va oxirgi raqamni eslab qoladi — busiz yopiq oyna fonda so'rov
 *   yuboraverardi.
 */
export function useCandidatePhoneMatches(phone: string, exceptId: string | null, enabled: boolean) {
  const digits = uzPhoneDigits(phone)

  return useQuery({
    queryKey: recruitingKeys.phoneMatches(digits, exceptId),
    queryFn: ({ signal }) =>
      fetchCandidatePhoneMatches(digits, exceptId ?? undefined, signal),
    enabled: enabled && digits.length === UZ_PHONE_LENGTH,
    /*
      Qayta urinish yo'q: bu yordamchi tekshiruv, forma usiz ham
      ishlaydi. Yiqilgan so'rov oynada qisqa izoh bo'lib ko'rinadi va
      saqlashni to'xtatmaydi (`candidate-form-dialog.tsx`).
    */
    retry: false,
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

/**
 * Vakansiyani almashtirish.
 *
 * `note` va `nextContactDate` IXTIYORIY: ularni yubormaydigan chaqiruv
 * avvalgidek ishlaydi (izoh yozuvi hosil bo'lmaydi, sana o'zgarmaydi).
 */
export function useReassignCandidate() {
  return useMutation({
    mutationFn: ({
      id,
      ...body
    }: {
      id: string
      vacancyId: string
      note?: string | null
      nextContactDate?: string | null
    }) => reassignCandidate(id, body),
  })
}

/**
 * ⭐ Suhbatga chaqirish — bosqich, vaqt va Telegram taklifi bir so'rovda.
 *
 * `useRecordStage` dan farqi bittada va u muhim: bu yerda nomzodga
 * XABAR ketadi. Shu sababli amal alohida hook va alohida oyna orqali
 * bajariladi — tasodifan bosilib qolmasligi kerak.
 *
 * `conclusion` bitta maydon bo'lib yuboriladi va uni qaysi jadvalga
 * yozishni SERVER hal qiladi (`stageId` bor — bosqich xulosasi, yo'q —
 * «suhbat ko'chirildi» izohi). Batafsil `inviteCandidateToInterview` da.
 */
export function useInviteToInterview() {
  return useMutation({
    mutationFn: ({
      id,
      ...body
    }: {
      id: string
      interviewAt: string
      stageId: string | null
      conclusion: string | null
      nextContactDate?: string | null
      clearNextContact?: boolean
    }) => inviteCandidateToInterview(id, body),
  })
}

/**
 * ⭐ Voronkaning yagona harakati — bosqich natijasini yozish.
 *
 * `nextContactDate` va `clearNextContact` IXTIYORIY va uch holatli:
 * sana — qo'yadi, `clearNextContact: true` — tozalaydi, ikkalasi ham
 * yo'q — TEGMAYDI (izoh `recruiting-api.ts` da, «keyingi aloqa sanasi:
 * yozish qoidasi»).
 *
 * Voronkadagi sudrash ham shu hook'ga keladi, lekin O'ZI yozmaydi:
 * kartani tashlash bosqich (yoki suhbat) oynasini ochadi va yagona
 * yozuv — o'sha oynadagi «Saqlash» (`candidate-funnel.tsx`,
 * `DroppedCardDialog`). Sudrashning alohida mutatsiyasi yo'q.
 */
export function useRecordStage() {
  return useMutation({
    mutationFn: ({
      id,
      ...body
    }: {
      id: string
      stageId: string
      date: string | null
      conclusion: string | null
      nextContactDate?: string | null
      clearNextContact?: boolean
    }) => recordCandidateStage(id, body),
  })
}

/**
 * ⭐ Tarixdagi bitta yozuvni tuzatish — `useRecordStage` ning egizagi.
 *
 * Ikkitasi ATAYLAB ajratilgan: birinchisi tarixga QATOR QO'SHADI
 * («yana gaplashdim»), ikkinchisi mavjud qatorni to'g'rilaydi
 * («o'sha qo'ng'iroqning natijasini yozdim»). Bitta hook'ga
 * ixtiyoriy `entryId` qo'shilsa, chaqiruv joyida qaysi amal
 * bajarilayotgani ko'rinmay qolardi — holbuki farqi tarixda qoladi.
 */
export function useEditStageEntry() {
  return useMutation({
    mutationFn: ({
      id,
      entryId,
      ...body
    }: {
      id: string
      entryId: string
      date: string | null
      conclusion: string | null
      nextContactDate?: string | null
      clearNextContact?: boolean
    }) => editCandidateStageEntry(id, entryId, body),
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
 * ishlatadi: ustunning O'ZI (`useFunnelColumn`) va sudralayotgan kartani
 * keshdan topadigan qidiruvchi (`useFunnelCardFinder`). Ikkinchisi keshni
 * AYNAN birinchisi yozgan kalit bo'yicha o'qishi shart — bitta maydon
 * (masalan `sort`) ikki joyda boshqacha yozilsa, qidiruv boshqa yozuvga
 * qarar va tashlangan karta «topilmay» qolardi. Bitta manba bunday
 * xatoni umuman imkonsiz qiladi.
 *
 * Tashlash keshni YAMAMAYDI: u bosqich (yoki suhbat) oynasini ochadi va
 * yagona yozuv — o'sha oynadagi «Saqlash». Karta yangi ustunga «Saqlash»
 * dan keyin o'tadi, `providers.tsx` keshni bekor qilganda.
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
 * Voronkadan chiqarish: rad etish, voz kechish, rezervga.
 *
 * ⚠️ `nextContactDate` ni FAQAT `reserve` hisobga oladi; qolgan
 * ikkitasida server uni e'tiborsiz qoldiradi va saqlangan sanani
 * tozalaydi. Shu sababli oynada maydon faqat rezerv uchun chizilishi
 * kerak — tip esa uchalasi uchun bitta (`closeCandidate` izohi).
 */
export function useCloseCandidate() {
  return useMutation({
    mutationFn: ({
      id,
      action,
      at,
      reason,
      nextContactDate,
    }: {
      id: string
      action: 'reject' | 'withdraw' | 'reserve'
      at: string | null
      reason: string | null
      nextContactDate?: string | null
    }) => closeCandidate(id, action, { at, reason, nextContactDate }),
  })
}

/**
 * ⭐ Voronkaga qaytarish — rezervdan, rad etilgandan va voz
 * kechgandan (2026-09-16 dan uchala manba).
 *
 * `note` — qaytarish sababi: rad etilgan va voz kechgan nomzodda
 * server uni TALAB qiladi, rezervda esa ixtiyoriy.
 */
export function useReturnCandidateToFunnel() {
  return useMutation({
    mutationFn: ({
      id,
      ...body
    }: {
      id: string
      note?: string | null
      nextContactDate?: string | null
    }) => returnCandidateToFunnel(id, body),
  })
}

/**
 * ⚠️ Nomzodni ishga olish — u XODIMGA aylanadi.
 *
 * Yagona joy, qayerda mutatsiya keshni O'ZI ham qo'shimcha bekor
 * qiladi: yaratilgan XODIM boshqa modullarning keshiga tegishli
 * (`employees`, `probations`) va HR darhol o'sha kartaga o'tishi
 * mumkin. `providers.tsx` dagi umumiy qoida ularni baribir qamrab
 * oladi — bu yerdagi ro'yxat NIYATNI hujjatlashtiradi: bitta amaldan
 * qaysi ekranlar eskirishi ko'rinib tursin.
 */
export function useHireCandidate() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: HireCandidateInput }) => hireCandidate(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: recruitingKeys.root })
      void queryClient.invalidateQueries({ queryKey: ['employees'] })
      void queryClient.invalidateQueries({ queryKey: ['probations'] })
    },
  })
}

/* ─────────────────────────────────────────────────────────────── fayllar */

/**
 * Bitta faylni yuklash.
 *
 * ⚠️ Chaqiruvchi tanlangan fayllarni KETMA-KET yuklaydi (server bitta
 * so'rovda bitta fayl qabul qiladi — sabab `uploadCandidateFile` da).
 * Shu sababli bu hook «bir amal — bir fayl» bo'lib qoladi va guruh
 * holatini (qaysi fayl yiqildi) chaqiruvchi o'zi kuzatadi: bitta yiqilish
 * qolganlarini to'xtatmasligi kerak.
 *
 * Kartani yangilash uchun QO'LDA `invalidateQueries` YOZILMAYDI —
 * `providers.tsx` har muvaffaqiyatli yozuvdan keyin butun keshni bekor
 * qiladi, ya'ni nomzod kartasi ham, ro'yxatdagi `hasResume` qisqichi ham
 * o'zi yangilanadi. Bu yerdagi qoida bitta rezyume paytidagidek qoldi:
 * qo'lda yozilgan bekor qilish ro'yxatlari doim eskirardi (sabab
 * `recruitingKeys` izohida).
 */
export function useUploadCandidateFile() {
  return useMutation({
    mutationFn: ({ id, file }: { id: string; file: File }) => uploadCandidateFile(id, file),
  })
}

/**
 * Faylni o'chirish — yozuv ham, diskdagi baytlar ham.
 *
 * Keshni bekor qilish yuqoridagi bilan bir xil: `providers.tsx` zimmasida.
 * Ochiq turgan RASM keshi (`fileBlob`) esa ataylab tegilmaydi — u `-photo`
 * kalitida va o'chirilgan faylga boshqa hech kim murojaat qilmaydi,
 * dialog yopilishi bilan blob'ga havola qolmaydi.
 */
export function useDeleteCandidateFile() {
  return useMutation({
    mutationFn: ({ id, fileId }: { id: string; fileId: string }) => deleteCandidateFile(id, fileId),
  })
}

/**
 * Faylni yuklab olish.
 *
 * NEGA `useQuery` emas, MUTATSIYA: fayl tugma bosilganda kerak bo'ladi,
 * sahifa ochilganda emas — keshda 20 MB baytni saqlab turishning
 * ma'nosi yo'q (`useDownloadDocument` bilan bir xil naqsh).
 *
 * `meta.readOnly` — bu amal serverda hech narsani o'zgartirmaydi, ya'ni
 * undan keyin butun keshni bekor qilish keraksiz (`providers.tsx`
 * dagi qoida shu bayroqni tekshiradi).
 */
export function useDownloadCandidateFile() {
  return useMutation({
    meta: { readOnly: true },
    mutationFn: async ({
      id,
      fileId,
      fallbackName,
    }: {
      id: string
      fileId: string
      fallbackName: string
    }) => {
      const { blob, disposition } = await fetchCandidateFile(id, fileId)
      // Nom SERVERNIKI: u asl fayl nomini biladi. Sarlavha kesilgan
      // bo'lsa (proksi, CORS) — chaqiruvchi bergan nom bilan saqlanadi.
      saveBlobAs(blob, fileNameFromDisposition(disposition) ?? fallbackName)
    },
  })
}

/**
 * Biriktirilgan RASMNI katta ko'rish uchun baytlar.
 *
 * NEGA bu yerda `useQuery` (yuklab olish esa mutatsiya): rasm oyna
 * OCHILGANDA kerak bo'ladi, tugma bosilganda emas — ya'ni bu ekranning
 * ma'lumoti va u yuklanish/xato holatiga ega bo'lishi kerak. Kesh esa
 * bitta rasmni ikki marta ochishni tekinga aylantiradi.
 *
 * ⚠️ PDF uchun ISHLATILMAYDI: ilovada PDF ko'ruvchi yo'q va u yuklab
 * olinadi (brauzer yoki tizim o'zi ochadi). Faqat `contentType` `image/`
 * bilan boshlanadigan qator bu hook'ni yoqadi — `enabled` shu tanlovni
 * chaqiruvchidan oladi, chunki turni bilgan joy u.
 *
 * Ro'yxatda MINIATYURA yo'q va shu sababli bu hook har qator uchun
 * chaqirilmaydi: o'nta miniatyura o'nta autentifikatsiyalangan so'rov
 * degani va u ekrandagi eng sekin narsa bo'lardi (SPEC §5.4.1).
 */
export function useCandidateFileBlob(candidateId: string, fileId: string, enabled: boolean) {
  return useQuery({
    queryKey: recruitingKeys.fileBlob(candidateId, fileId),
    queryFn: async ({ signal }) => {
      const { blob } = await fetchCandidateFile(candidateId, fileId, signal)
      return blob
    },
    enabled: enabled && candidateId !== '' && fileId !== '',
    /** Bir fayl — bir mazmun. Fayllar o'zgarmaydi: yangisi yangi ID oladi. */
    staleTime: Infinity,
    /**
     * Qayta urinish yo'q: rasm chiqmasa oynada xato ko'rinadi va yuklab
     * olish tugmasi baribir ishlaydi (`useCandidatePhoto` bilan bir xil).
     */
    retry: false,
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
