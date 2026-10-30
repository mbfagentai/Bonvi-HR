import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'

/*
 * Ma'lumotnomalar S2 modulida qurilgan va uning `index.ts` si so'rov
 * hook'larini tashqariga chiqarmaydi — u faqat sahifalarni beradi.
 *
 * Shu sababli import bu yerda «chuqur» ketadi. Bu bilib qilingan yagona
 * chetlanish va u AYNAN SHU FAYL bilan chegaralangan: xodimlar modulining
 * qolgan qismi ma'lumotnomalar ichki tuzilishini bilmaydi. S2 barrel'iga
 * eksport qo'shilsa — o'zgartiriladigan joy bitta.
 */
import {
  useCatalogList,
  useDepartmentTree,
  useDuties,
} from '@/features/catalog/api/catalog-queries'
import type {
  CatalogItem,
  DepartmentNode,
  PositionItem,
} from '@/features/catalog/api/catalog-api'
import { flattenTree } from '@/features/catalog/lib/tree'
import { saveBlobAs } from '@/shared/lib/download'
import type { SelectOption } from '@/shared/ui'

import {
  addAdditionalDuty,
  changeSalary,
  editEmploymentEvent,
  endAdditionalDuty,
  closeFlag,
  createFlag,
  deleteEmployeePhoto,
  deleteEmployeeCheckFile,
  exportEmployees,
  fetchEmployee,
  fetchEmployeePhoto,
  fetchEmployeeStages,
  fetchEmployees,
  fetchFlags,
  fetchEmployeeCheckFileContent,
  fetchEmployeeCheckFiles,
  fetchTimeline,
  hire,
  terminate,
  transfer,
  updateEmployee,
  uploadEmployeePhoto,
  uploadEmployeeCheckFile,
  type AdditionalDutyRequest,
  type ChangeSalaryRequest,
  type EditEmploymentEventRequest,
  type CreateFlagRequest,
  type EmployeeDetails,
  type EmployeeFilterParams,
  type EmployeeListFilters,
  type HireRequest,
  type NamedRef,
  type TerminateRequest,
  type TransferRequest,
  type UpdateEmployeeRequest,
} from './employees-api'
import {
  fetchEmployeeCompetency,
  saveEmployeeCompetency,
  type SaveCompetencyRequest,
} from './employee-competency-api'
import {
  createIncentive,
  deleteIncentive,
  fetchIncentives,
  updateIncentive,
  type GrantIncentiveRequest,
  type IncentiveKindValue,
  type IncentiveTypeItem,
  type UpdateIncentiveRequest,
} from './incentives-api'
import {
  deleteDocument,
  fetchDocumentFile,
  fetchDocuments,
  updateDocument,
  uploadDocument,
  type EmployeeDocument,
  type UpdateDocumentRequest,
} from './documents-api'

/**
 * Kesh kalitlari.
 *
 * Ro'yxat kaliti filtrlarni to'liq o'z ichiga oladi: filtr o'zgarsa —
 * yangi so'rov, lekin eskisi keshda qoladi. HR filtrlar orasida tez-tez
 * u yoqdan-bu yoqqa yuradi, shu sababli qaytishda ro'yxat darhol chiqadi.
 */
export const employeeKeys = {
  root: ['employees'] as const,
  /** Barcha ro'yxat so'rovlari — filtrdan qat'i nazar bekor qilish uchun. */
  lists: ['employees', 'list'] as const,
  list: (filters: EmployeeListFilters) => ['employees', 'list', filters] as const,
  details: (id: string) => ['employees', 'details', id] as const,
  timeline: (id: string) => ['employees', 'timeline', id] as const,
  flags: (id: string) => ['employees', 'flags', id] as const,
  competency: (id: string) => ['employees', 'competency', id] as const,
  /**
   * `kind` KIRITILMAGAN — bekor qilishda ishlatiladi: prefiks sifatida
   * `kind`li kalitlarning HAMMASINI ham qamrab oladi (TanStack Query
   * standart mos kelishi — massiv boshlanishi bo'yicha).
   */
  incentives: (id: string) => ['employees', 'incentives', id] as const,
  incentivesByKind: (id: string, kind: IncentiveKindValue) =>
    ['employees', 'incentives', id, kind] as const,

  /** Hujjatlar tabi (S6). */
  documents: (id: string) => ['employees', 'documents', id] as const,
  /**
   * Hujjat baytlari — ko'rish oynasi uchun (`FileViewer`). Ildiz `-photo` bilan tugaydi:
   * `providers.tsx` muvaffaqiyatli yozuvdan keyin keshni bekor qilganda bu guruhga tegmaydi —
   * fayl o'zgarmaydi (tahrir faqat tur va izohni almashtiradi), qayta yuklash ortiqcha.
   */
  documentFile: (id: string, documentId: string) => ['employee-document-file-photo', id, documentId] as const,

  /**
   * Rasm keshi ATAYLAB `employees` ildizidan tashqarida turadi.
   *
   * Aks holda har bir ishga qabul yoki belgi qo'shish butun ildizni bekor
   * qilib, sahifadagi 25 ta rasmni ham qaytadan yuklatardi. Rasm esa
   * xodim yozuvidan mustaqil: u faqat `photoKey` o'zgarganda o'zgaradi.
   */
  photos: (id: string) => ['employee-photo', id] as const,
  photo: (id: string, photoKey: string | null) =>
    ['employee-photo', id, photoKey] as const,

  /** Tekshiruv materiallari — faqat metama'lumot (nom, tur, hajm, sana). */
  checkFiles: (id: string) => ['employee-check-files', id] as const,

  /**
   * Bitta materialning BAYTLARI — RASM KESHI.
   *
   * Kalit ildizi ataylab `-photo` bilan tugaydi (PDF bo'lsa ham):
   * `providers.tsx` har qanday muvaffaqiyatli mutatsiyadan keyin butun
   * keshni bekor qiladi va shu qoidadan CHETLAB O'TADIGAN yagona guruh —
   * nomi `-photo` ga tugaydigan binar keshlar. Material ham aynan shunday:
   * bir necha megabaytlik blob va u HECH QACHON o'zgarmaydi (yangi fayl —
   * yangi `id`). Chetlab o'tmasa, kartadagi HAR bir yozuv amali ochiq
   * turgan faylni qaytadan tortib olardi.
   */
  checkFileContents: (id: string) => ['employee-check-file-photo', id] as const,
  checkFileContent: (id: string, fileId: string | null) =>
    ['employee-check-file-photo', id, fileId] as const,

  /** Nomzodlik bosqichlari — «Kengash xulosasi» (faqat o'qish). */
  stages: (id: string) => ['employees', 'stages', id] as const,
}

/**
 * @param enabled - `false` bo'lsa so'rov yuborilmaydi.
 *
 * NEGA kerak: ro'yxat bo'sh chiqqanda sinovdagilar SONI ham so'raladi
 * («hammasi sinovda» degan bo'sh holat matni uchun). U so'rov har
 * ochilishda emas, faqat ro'yxat haqiqatan bo'sh bo'lganda ketishi kerak.
 */
export function useEmployees(filters: EmployeeListFilters, enabled = true) {
  return useQuery({
    queryKey: employeeKeys.list(filters),
    queryFn: ({ signal }) => fetchEmployees(filters, signal),
    enabled,
    /**
     * Sahifadan sahifaga o'tganda jadval bo'shab, keyin qayta to'lmaydi —
     * eski ma'lumot ko'rinib turadi va faqat ustidagi holat «yangilanmoqda»
     * bo'ladi. Sahifalangan ro'yxatda bu eng muhim detal: ekran sakramaydi.
     */
    placeholderData: (previous) => previous,
  })
}

export function useEmployee(id: string) {
  return useQuery({
    queryKey: employeeKeys.details(id),
    queryFn: ({ signal }) => fetchEmployee(id, signal),
    enabled: id !== '',
  })
}

export function useEmployeeTimeline(id: string) {
  return useQuery({
    queryKey: employeeKeys.timeline(id),
    queryFn: ({ signal }) => fetchTimeline(id, signal),
    enabled: id !== '',
  })
}

export function useEmployeeFlags(id: string) {
  return useQuery({
    queryKey: employeeKeys.flags(id),
    queryFn: ({ signal }) => fetchFlags(id, signal),
    enabled: id !== '',
  })
}

/**
 * Xodim rasmi.
 *
 * Kalitda `photoKey` turadi — bu keshni o'z-o'zidan to'g'ri saqlaydi:
 * rasm almashtirilsa kalit ham boshqa bo'ladi va eski blob'ga hech kim
 * murojaat qilmaydi. Ya'ni keshni qo'lda tozalash kerak emas.
 */
export function useEmployeePhoto(employeeId: string, photoKey: string | null, enabled = true) {
  return useQuery({
    queryKey: employeeKeys.photo(employeeId, photoKey),
    queryFn: ({ signal }) => fetchEmployeePhoto(employeeId, signal),
    /**
     * Kaliti yo'q — rasmi ham yo'q, so'rov umuman yuborilmaydi.
     * Busiz 25 qatorli sahifa har ochilganda 25 ta keraksiz 404 ketardi.
     *
     * `enabled = false` faqat YANGI so'rovni to'xtatadi: keshda bor rasm
     * baribir darhol qaytadi (`employee-photo.tsx`, virtual ro'yxat).
     */
    enabled: enabled && employeeId !== '' && photoKey !== null,
    /** Bir kalit — bir rasm. U o'zgarmaydi, demak eskirmaydi ham. */
    staleTime: Infinity,
    /**
     * Qayta urinish yo'q: rasm chiqmasa bosh harflar qoladi va HR ishida
     * hech narsa to'xtamaydi. Muvaffaqiyatsiz so'rovni takrorlash esa
     * ro'yxatni yuklashni sekinlashtiradi.
     */
    retry: false,
  })
}

/**
 * Rasmni yuklash.
 *
 * Rasm ro'yxatda ham, kartada ham ko'rinadi — shu sababli ikkalasi ham
 * bekor qilinadi. Kartadan kelgan yangi `photoKey` esa rasm so'rovini
 * o'zi qayta ishga tushiradi.
 */
export function useUploadEmployeePhoto(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file: File) => uploadEmployeePhoto(employeeId, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.details(employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.lists })

      /*
       * Qo'shimcha ehtiyot chorasi: server kalitni O'ZGARTIRMASDAN faylni
       * almashtirsa (masalan kalit xodim `id` sidan hosil qilinsa), yangi
       * kalit eskisi bilan bir xil bo'ladi va keshdagi eski rasm ko'rinib
       * qolardi. Shu sababli shu xodimning barcha rasm yozuvlari bekor
       * qilinadi — shartnomaning bu qismi qanday hal bo'lishidan qat'i nazar.
       */
      void queryClient.invalidateQueries({ queryKey: employeeKeys.photos(employeeId) })
    },
  })
}

/**
 * Rasmni o'chirish.
 *
 * Keshdagi blob `invalidate` emas, `remove` qilinadi: `photoKey` `null`
 * bo'lgach unga hech kim murojaat qilmaydi, lekin qayta so'rov yuborishning
 * ham ma'nosi yo'q — u faqat 404 olib keladi.
 */
export function useDeleteEmployeePhoto(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => deleteEmployeePhoto(employeeId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.details(employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.lists })
      queryClient.removeQueries({ queryKey: employeeKeys.photos(employeeId) })
    },
  })
}

/* ═══════════════════════════════════════════════ mehnat.uz (havola + skrinshot) */

/**
 * «Tekshiruvlar» kartasining materiallari — ro'yxat, baytlarsiz.
 */
export function useEmployeeCheckFiles(employeeId: string) {
  return useQuery({
    queryKey: employeeKeys.checkFiles(employeeId),
    queryFn: ({ signal }) => fetchEmployeeCheckFiles(employeeId, signal),
    enabled: employeeId !== '',
  })
}

/**
 * Bitta materialning fayli — faqat HR uni OCHGANDA so'raladi.
 *
 * @param fileId - `null` — hech narsa ochilmagan, so'rov yuborilmaydi.
 */
export function useEmployeeCheckFileContent(employeeId: string, fileId: string | null) {
  return useQuery({
    queryKey: employeeKeys.checkFileContent(employeeId, fileId),
    queryFn: ({ signal }) => fetchEmployeeCheckFileContent(employeeId, fileId ?? '', signal),
    enabled: employeeId !== '' && fileId !== null,
    /** Bir `id` — bir fayl. U o'zgarmaydi, demak eskirmaydi ham. */
    staleTime: Infinity,
    /**
     * Qayta urinish yo'q — rasmdagi bilan bir xil mulohaza: fayl chiqmasa
     * oyna xatoni ko'rsatadi, takroriy so'rov esa faqat kutishni uzaytiradi.
     */
    retry: false,
  })
}

/** Bir nechta materialni yuklash natijasi — yiqilgan fayllar ro'yxati. */
export interface CheckFilesUploadResult {
  failed: { file: File; error: unknown }[]
}

/**
 * Bir nechta materialni yuklash — BITTA amal, ichida fayllar KETMA-KET.
 *
 * NEGA bitta mutatsiya, har fayl uchun alohida emas. `providers.tsx` har
 * bir muvaffaqiyatli mutatsiyadan keyin butun keshni bekor qiladi. Fayllar
 * alohida mutatsiya bo'lsa, besh fayl kartani besh marta qayta yuklatardi
 * va har yangi bekor qilish oldingisining hali tugamagan so'rovlarini
 * uzib tashlardi (serverda «bekor qilingan so'rov» jurnali). Bitta amal —
 * bitta yangilanish.
 *
 * NEGA ketma-ket: server bitta so'rovda bitta faylni oladi, parallel
 * yuborish esa 20 MB lik fayllar bilan tarmoqni bo'g'ib qo'yardi.
 *
 * Bitta fayl yiqilsa qolganlari baribir yuklanadi — amal xato bilan
 * TUGAMAYDI, yiqilganlar natijada qaytadi (HR qaysi fayl o'tmaganini
 * nomi bilan ko'rishi kerak).
 *
 * ⚠️ XODIM O'ZGARUVCHILARDA keladi, hook yopig'idan (closure) emas.
 * Yuklash davom etayotganda HR boshqa kartaga o'tishi mumkin (bildirishnoma,
 * «orqaga») — sahifa qayta o'rnatilmaydi, faqat `employeeId` o'zgaradi.
 * Qolgan fayllar o'shanda boshqa odamga — masalan sudlanganlik
 * ma'lumotnomasi begona xodimga — yuklanib ketardi (tekshiruv, 2026-09-27).
 * Xodim amal BOSHLANGAN lahzada qotiriladi.
 */
export function useUploadEmployeeCheckFiles() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      employeeId,
      files,
      onProgress,
    }: {
      employeeId: string
      files: File[]
      onProgress: (done: number) => void
    }): Promise<CheckFilesUploadResult> => {
      const failed: CheckFilesUploadResult['failed'] = []

      for (const [index, file] of files.entries()) {
        try {
          await uploadEmployeeCheckFile(employeeId, file)
        } catch (error) {
          failed.push({ file, error })
        }
        onProgress(index + 1)
      }

      return { failed }
    },
    onSuccess: (_, { employeeId }) => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.checkFiles(employeeId) })
    },
  })
}

/**
 * Materialni o'chirish.
 *
 * Keshdagi blob `remove` qilinadi: o'chirilgan `id` ga hech kim murojaat
 * qilmaydi, qayta so'rov esa faqat 404 olib kelardi
 * (`useDeleteEmployeePhoto` dagi bilan bir xil).
 */
export function useDeleteEmployeeCheckFile(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (fileId: string) => deleteEmployeeCheckFile(employeeId, fileId),
    onSuccess: (_, fileId) => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.checkFiles(employeeId) })
      queryClient.removeQueries({ queryKey: employeeKeys.checkFileContent(employeeId, fileId) })
    },
  })
}

/* ══════════════════════════════════════════ kengash xulosasi (faqat o'qish) */

/**
 * Xodimning nomzodlik bosqichlari.
 *
 * @param enabled - `false` bo'lsa so'rov yuborilmaydi.
 *
 * NEGA kerak: endpoint `recruiting.view` talab qiladi va bu ruxsat
 * `employees.view` dan MUSTAQIL. Ruxsatsiz foydalanuvchida so'rov
 * yuborilsa, u har karta ochilganda 403 olib kelardi — bo'lim esa
 * baribir chizilmaydi.
 */
export function useEmployeeStages(employeeId: string, enabled = true) {
  return useQuery({
    queryKey: employeeKeys.stages(employeeId),
    queryFn: ({ signal }) => fetchEmployeeStages(employeeId, signal),
    enabled: enabled && employeeId !== '',
  })
}

/**
 * Ishga qabul.
 *
 * Muvaffaqiyatdan keyin butun `employees` ildizi bekor qilinadi: yangi
 * xodim ro'yxatda, sinovdagilar sanog'ida va (qayta ishga olish bo'lsa)
 * o'sha shaxs kartasida bir vaqtda paydo bo'lishi kerak.
 *
 * <b>Sinov ro'yxati ham bekor qilinadi.</b> Yangi xodim endi ISTISNOSIZ
 * sinovdan boshlaydi, ya'ni u paydo bo'ladigan birinchi ekran — `/sinov`,
 * va forma aynan o'sha sahifadan ochiladi. Busiz HR odam qo'shib,
 * ko'z oldidagi ro'yxatda hech narsa o'zgarmaganini ko'rardi.
 *
 * Kalit ATAYLAB xom massiv, `probationKeys.root` emas: `probations-queries`
 * shu fayldan `employeeKeys` ni oladi va teskari import aylanma bog'liqlik
 * hosil qilardi. Bu yagona joy, shu sababli takrorlanish ham bitta.
 */
export function useHire() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: HireRequest) => hire(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })
      void queryClient.invalidateQueries({ queryKey: ['probations'] })
      // Qabul «rezerv» belgisini YOPADI (`HireEmployeeCommandHandler`) —
      // `/rezerv` ro'yxati ham yangilanishi kerak.
      void queryClient.invalidateQueries({ queryKey: ['flagged'] })
    },
  })
}

/**
 * Xodim kartasini tahrirlash.
 *
 * Ikkita joy bekor qilinadi va ikkalasi ham kerak:
 *
 *  • <b>karta</b> — tahrir qilingan maydonlar aynan shu yerda ko'rinadi;
 *  • <b>ro'yxat</b> — jadvalda JSHSHIR, telefon va «tekshirish kerak»
 *    ustunlari bor. Server saqlashda o'sha belgini o'zi olib tashlaydi,
 *    ya'ni ro'yxat yangilanmasa HR tuzatgan yozuv «tekshirish kerak»
 *    filtrida turaverardi va u qayta-qayta ochilardi.
 *
 * Tarix (`timeline`) tegilmaydi: shaxs ma'lumotini tuzatish mehnat
 * yo'lida hodisa yaratmaydi.
 */
export function useUpdateEmployee(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: UpdateEmployeeRequest) => updateEmployee(employeeId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.details(employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.lists })
    },
  })
}

export function useTerminate(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      employmentId,
      body,
      confirm,
    }: {
      employmentId: string
      body: TerminateRequest
      confirm: boolean
    }) => terminate(employmentId, body, confirm),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.details(employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.timeline(employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.lists })

      /*
        Bo'shatish BELGILARGA ham tegadi: server ishlab turganlar uchun
        qo'yilgan belgilarni («nazoratda», «qobiliyatli») yopadi
        (`TerminateEmploymentCommandHandler`). Busiz kartadagi nishon va
        «Belgilar» tabi yopilgan belgini ochiqdek ko'rsatib turardi —
        ya'ni HR bo'shatgandan keyin ham «nazoratda» yozuvini ko'rardi.
        `flagged` esa ro'yxat sahifalari (`/nazorat`, `/qobiliyatli`).
      */
      void queryClient.invalidateQueries({ queryKey: employeeKeys.flags(employeeId) })
      void queryClient.invalidateQueries({ queryKey: ['flagged'] })
    },
  })
}

/**
 * Mehnat munosabatidagi o'zgarishlar: ko'chirish, qo'shimcha vazifa, oklad.
 *
 * Uchalasi bir xil narsani bekor qiladi va shu sababli bitta yordamchidan
 * yasaladi. Bekor qilinadigan joylar:
 *
 *  • <b>karta</b> — lavozim, bo'lim va oklad aynan shu yerda ko'rinadi;
 *  • <b>tarix</b> — har bir amal yangi hodisa yozadi;
 *  • <b>ro'yxat</b> — jadvalda lavozim va bo'lim ustunlari bor.
 *
 * Uchtasidan biri unutilsa, HR o'zgarishni kiritib, ekranda eskisini
 * ko'rib turardi — bu esa amal bajarilmagandek taassurot beradi.
 */
function useEmploymentChange<TBody>(
  employeeId: string,
  action: (employmentId: string, body: TBody) => Promise<void>,
) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ employmentId, body }: { employmentId: string; body: TBody }) =>
      action(employmentId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.details(employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.timeline(employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.lists })
    },
  })
}

export function useTransfer(employeeId: string) {
  return useEmploymentChange<TransferRequest>(employeeId, transfer)
}

export function useAdditionalDuty(employeeId: string) {
  return useEmploymentChange<AdditionalDutyRequest>(employeeId, addAdditionalDuty)
}

export function useChangeSalary(employeeId: string) {
  return useEmploymentChange<ChangeSalaryRequest>(employeeId, changeSalary)
}

/**
 * ⭐ Mehnat yo'lidagi yozuvning terish xatosini tuzatish.
 *
 * `useEmploymentChange` dan foydalanadi, chunki bekor qilinadigan joylar
 * AYNAN o'sha: tuzatish joriy okladni ham yangilashi mumkin (yozuv
 * okladning manbai bo'lsa), ya'ni karta ham, ro'yxat ham eskirgan
 * bo'lib qoladi.
 *
 * Yozuv ID'si tanada emas, funksiyaning O'ZIDA: u resursning
 * identifikatori (`PUT .../events/{eventId}`), ma'lumot emas.
 */
export function useEditEmploymentEvent(employeeId: string, eventId: string) {
  return useEmploymentChange<EditEmploymentEventRequest>(
    employeeId,
    (employmentId, body) => editEmploymentEvent(employmentId, eventId, body),
  )
}

/**
 * Qo'shimcha vazifani tugatish yoki qayta ochish.
 *
 * `useEmploymentChange` dan — bekor qilinadigan joylar AYNAN o'sha: karta
 * (faol vazifalar va jami) va mehnat yo'li (vazifa davri).
 */
export function useEndAdditionalDuty(employeeId: string, eventId: string) {
  return useEmploymentChange<{ endDate: string | null }>(
    employeeId,
    (employmentId, body) => endAdditionalDuty(employmentId, eventId, body.endDate),
  )
}

export function useCreateFlag(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: CreateFlagRequest) => createFlag(employeeId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.flags(employeeId) })
      // Ro'yxatdagi ogohlantirish belgisi ham shu ma'lumotdan chiziladi
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })
      /*
        Belgi ro'yxatlari (`/rezerv`, `/qora-royxat`, `/nazorat`,
        `/qobiliyatli`) BOSHQA kalitda yashaydi. Ular bekor qilinmasa,
        HR kartada belgi qo'yib, ro'yxatga o'tganda odamni topa olmasdi
        va yozuv saqlanmagan deb o'ylardi.
      */
      void queryClient.invalidateQueries({ queryKey: ['flagged'] })
    },
  })
}

export function useCloseFlag(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      flagId,
      closedAt,
      note,
    }: {
      flagId: string
      closedAt: string
      note: string | null
    }) => closeFlag(employeeId, flagId, closedAt, note),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.flags(employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })
      void queryClient.invalidateQueries({ queryKey: ['flagged'] })
    },
  })
}

/* ══════════════════════════════════════════════════════ qobiliyatlar (91.15) */

/**
 * Qobiliyatlar profili.
 *
 * Javob `null` bo'lishi mumkin — HR hali to'ldirmagan (`isError` EMAS).
 * Chaqiruvchi buni «forma bo'sh boshlanadi» deb o'qiydi.
 */
export function useEmployeeCompetency(employeeId: string) {
  return useQuery({
    queryKey: employeeKeys.competency(employeeId),
    queryFn: ({ signal }) => fetchEmployeeCompetency(employeeId, signal),
    enabled: employeeId !== '',
  })
}

/**
 * Profilni saqlash. Javob keshka to'g'ridan-to'g'ri yoziladi
 * (`invalidate` emas, `setQueryData`) — server yangilangan TO'LIQ
 * profilni qaytaradi, qo'shimcha `GET` so'rovi kerak emas.
 */
export function useSaveEmployeeCompetency(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: SaveCompetencyRequest) => saveEmployeeCompetency(employeeId, body),
    onSuccess: (data) => {
      queryClient.setQueryData(employeeKeys.competency(employeeId), data)
    },
  })
}

/* ═══════════════════════════════════════════════ rag'bat va jarima (M8) */

/** `kind` berilmasa — hammasi (rag'bat + jarima). */
export function useIncentives(employeeId: string, kind?: IncentiveKindValue) {
  return useQuery({
    queryKey:
      kind === undefined
        ? employeeKeys.incentives(employeeId)
        : employeeKeys.incentivesByKind(employeeId, kind),
    queryFn: ({ signal }) => fetchIncentives(employeeId, kind, signal),
    enabled: employeeId !== '',
  })
}

/**
 * Tur ro'yxati — `award-types` yoki `penalty-types` ma'lumotnomasidan,
 * `kind` ga qarab. Ma'lumotnomalar S2 modulida qurilgan, shu sababli
 * uning tayyor `useCatalogList` hook'i to'g'ridan-to'g'ri qayta
 * ishlatiladi (`useFilterOptions` dagi bilan bir xil naqsh).
 *
 * Faqat FAOL turlar: faolsizlantirilgan turga yangi yozuv berib
 * bo'lmaydi.
 */
export function useIncentiveTypes(kind: IncentiveKindValue) {
  return useCatalogList<IncentiveTypeItem>(kind === 'award' ? 'award-types' : 'penalty-types', true)
}

/**
 * Rag'bat/jarima ro'yxatlarini bekor qilish.
 *
 * `employeeKeys.incentives(id)` PREFIKS sifatida ishlatiladi — `kind`li
 * (filtrlangan) va `kind`siz (to'liq) so'rovlarning HAMMASI bir yo'la
 * bekor qilinadi, chunki ularning kaliti shu prefiksdan boshlanadi.
 */
function useInvalidateIncentives(employeeId: string) {
  const queryClient = useQueryClient()

  return () => {
    void queryClient.invalidateQueries({ queryKey: employeeKeys.incentives(employeeId) })
  }
}

export function useCreateIncentive(employeeId: string) {
  const invalidate = useInvalidateIncentives(employeeId)

  return useMutation({
    mutationFn: (body: GrantIncentiveRequest) => createIncentive(employeeId, body),
    onSuccess: invalidate,
  })
}

export function useUpdateIncentive(employeeId: string) {
  const invalidate = useInvalidateIncentives(employeeId)

  return useMutation({
    mutationFn: ({
      incentiveId,
      body,
    }: {
      incentiveId: string
      body: UpdateIncentiveRequest
    }) => updateIncentive(employeeId, incentiveId, body),
    onSuccess: invalidate,
  })
}

export function useDeleteIncentive(employeeId: string) {
  const invalidate = useInvalidateIncentives(employeeId)

  return useMutation({
    mutationFn: (incentiveId: string) => deleteIncentive(employeeId, incentiveId),
    onSuccess: invalidate,
  })
}

/* ══════════════════════════════════════════════════════════ hujjatlar (S6) */

/** Hamma hujjat — guruhlash (tur bo'yicha) chaqiruvchi tomonda, klientda. */
export function useEmployeeDocuments(employeeId: string) {
  return useQuery({
    queryKey: employeeKeys.documents(employeeId),
    queryFn: ({ signal }) => fetchDocuments(employeeId, undefined, signal),
    enabled: employeeId !== '',
  })
}

/**
 * Hujjat turlari — S2 «document-types» ma'lumotnomasidan, `useIncentiveTypes`
 * dagi bilan bir xil naqsh (yuqoridagi izohga qarang). Faqat FAOL turlar:
 * faolsizlantirilgan turga yangi hujjat biriktirib bo'lmaydi.
 */
export function useDocumentTypes() {
  return useCatalogList<CatalogItem>('document-types', true)
}

function useInvalidateDocuments(employeeId: string) {
  const queryClient = useQueryClient()

  return () => {
    void queryClient.invalidateQueries({ queryKey: employeeKeys.documents(employeeId) })
  }
}

export function useUploadDocument(employeeId: string) {
  const invalidate = useInvalidateDocuments(employeeId)

  return useMutation({
    mutationFn: ({
      file,
      documentTypeId,
      note,
    }: {
      file: File
      documentTypeId: string
      note: string | null
    }) => uploadDocument(employeeId, file, documentTypeId, note),
    onSuccess: invalidate,
  })
}

export function useUpdateDocument(employeeId: string) {
  const invalidate = useInvalidateDocuments(employeeId)

  return useMutation({
    mutationFn: ({
      documentId,
      body,
    }: {
      documentId: string
      body: UpdateDocumentRequest
    }) => updateDocument(employeeId, documentId, body),
    onSuccess: invalidate,
  })
}

export function useDeleteDocument(employeeId: string) {
  const invalidate = useInvalidateDocuments(employeeId)

  return useMutation({
    mutationFn: (documentId: string) => deleteDocument(employeeId, documentId),
    onSuccess: invalidate,
  })
}

/**
 * Hujjatni ko'rish/yuklab olish.
 *
 * Kesh bilan bog'liq emas — shu sababli boshqalardan farqli, `onSuccess`
 * hech narsani bekor qilmaydi, bayt kelgach BROWSER'GA saqlanadi
 * (`saveBlobAs`). Bitta mutatsiya butun tab uchun umumiy: qaysi qator
 * yuklanayotganini chaqiruvchi `variables?.id` bilan aniqlaydi (`incentives`
 * uslubidagi kabi alohida holat kerak emas).
 */
/** Hujjatni ko'rish uchun baytlar — `useEmployeeCheckFileContent` bilan bir xil qoida. */
export function useDocumentFileBlob(employeeId: string, documentId: string | null) {
  return useQuery({
    queryKey: employeeKeys.documentFile(employeeId, documentId ?? ''),
    queryFn: ({ signal }) => fetchDocumentFile(employeeId, documentId ?? '', signal),
    enabled: employeeId !== '' && documentId !== null,
    staleTime: Infinity,
    retry: false,
  })
}

export function useDownloadDocument(employeeId: string) {
  return useMutation({
    mutationFn: (doc: EmployeeDocument) =>
      fetchDocumentFile(employeeId, doc.id).then((blob) => ({ blob, doc })),
    onSuccess: ({ blob, doc }) => saveBlobAs(blob, doc.fileName),
  })
}

/* ═══════════════════════════════════════════════════════ Excel eksport */

/**
 * Xodimlar ro'yxatini Excel'ga yuklash.
 *
 * NEGA `useQuery` emas, `useMutation`: so'rov hech qanday holatni
 * o'zgartirmasa ham, u FOYDALANUVCHI BOSGANDA va faqat bir marta ketishi
 * kerak. `useQuery` sahifa ochilishining o'zida 5000 qatorlik faylni
 * so'rab yuborardi, oyna fokusga qaytganda esa yana takrorlardi — server
 * eksportga tezlik cheklovi qo'ygan (60 soniyada 5 ta), ya'ni bunday
 * so'rovlar HR ning haqiqiy urinishini 429 bilan to'sib qo'yardi.
 *
 * Kesh ham bekor qilinmaydi (hujjat yuklab olishdagi bilan bir xil
 * sabab): natija ekranga emas, DISKKA tushadi.
 */
export function useExportEmployees() {
  return useMutation({
    mutationFn: (filters: EmployeeFilterParams) => exportEmployees(filters),
    onSuccess: ({ blob, fileName }) => saveBlobAs(blob, fileName),
    /*
      Eksport serverda HECH NARSANI o'zgartirmaydi — u mutatsiya shaklida
      yozilgan faqat shu sababli: tugma bosilganda ishga tushadi, so'rov
      esa avtomatik takrorlanmasligi kerak.

      `readOnly` bayrog'i uni `providers.tsx` dagi umumiy kesh bekor
      qilishdan chetlab o'tkazadi (izohi o'sha yerda): fayl yuklab
      olingani uchun butun ekranni qayta yuklashning ma'nosi yo'q.
    */
    meta: { readOnly: true },
  })
}

/* ══════════════════════════════════════════ ma'lumotnomalar (filtr uchun) */

const EMPTY_TREE: DepartmentNode[] = []
const EMPTY_LIST: CatalogItem[] = []

export interface FilterOptions {
  departments: SelectOption[]
  positions: SelectOption[]
  directions: SelectOption[]
  legalEntities: SelectOption[]
  /** Lavozim `id` → bo'lim `id`: forma bo'limni o'zi to'ldirishi uchun. */
  positionDepartment: Map<string, string>
  isPending: boolean
}

/**
 * Xodim kartasidagi yo'nalishlar — nomi bilan.
 *
 * Karta javobi (`EmployeeDto`) yo'nalishlarni faqat `directionIds` bilan
 * beradi, nomsiz, va shu sababli karta har doim «belgilanmagan» deb
 * turardi. Nomlar ma'lumotnomadan olinadi. Faolsizlantirilgan yo'nalish
 * ham xodimda qolishi mumkin, shu sababli HAMMASI so'raladi.
 */
export function useEmployeeDirections(employee: EmployeeDetails): NamedRef[] {
  const catalog = useCatalogList('directions', false)

  return useMemo(() => {
    if (employee.directions.length > 0) return employee.directions
    const names = new Map((catalog.data ?? EMPTY_LIST).map((item) => [item.id, item.name]))
    return employee.directionIds.flatMap((id) => {
      const name = names.get(id)
      return name === undefined ? [] : [{ id, name }]
    })
  }, [employee.directions, employee.directionIds, catalog.data])
}

/**
 * Filtrlar va ishga qabul formasi uchun ma'lumotnoma ro'yxatlari.
 *
 * Faqat FAOL yozuvlar so'raladi: faolsizlantirilgan lavozimga yangi odam
 * qo'yib bo'lmaydi, ro'yxatda esa u chalkashtiradi. Eski xodimda bunday
 * lavozim uchrasa — kartada nomi baribir ko'rinadi (u xodim yozuvidan
 * keladi, ma'lumotnomadan emas).
 */
export function useFilterOptions(): FilterOptions {
  const departments = useDepartmentTree(true)
  const positions = useCatalogList<PositionItem>('positions', true)
  const directions = useCatalogList('directions', true)
  const legalEntities = useCatalogList('legal-entities', true)

  return useMemo(() => {
    const positionItems = positions.data ?? (EMPTY_LIST as PositionItem[])

    return {
      // Daraxt yassilanadi, chuqurlik `depth` orqali bo'sh joy bilan ko'rinadi
      departments: flattenTree(departments.data ?? EMPTY_TREE).map((row) => ({
        value: row.node.id,
        label: row.node.name,
        depth: row.depth,
      })),
      positions: positionItems.map((item) => ({ value: item.id, label: item.name })),
      directions: (directions.data ?? EMPTY_LIST).map((item) => ({
        value: item.id,
        label: item.name,
      })),
      legalEntities: (legalEntities.data ?? EMPTY_LIST).map((item) => ({
        value: item.id,
        label: item.name,
      })),
      positionDepartment: new Map(
        positionItems.flatMap((item) =>
          item.departmentId === null ? [] : [[item.id, item.departmentId] as const],
        ),
      ),
      isPending:
        departments.isPending ||
        positions.isPending ||
        directions.isPending ||
        legalEntities.isPending,
    }
  }, [
    departments.data,
    departments.isPending,
    positions.data,
    positions.isPending,
    directions.data,
    directions.isPending,
    legalEntities.data,
    legalEntities.isPending,
  ])
}

/**
 * Tanlangan lavozimning vazifalari.
 *
 * HR ning talabi: «лавозимни танлаганда автоматически вазифаси ҳам
 * юкланади» — ishga qabul formasida lavozim tanlangan zahoti uning
 * vazifalari ko'rinadi va shartnomaga nima tushishi darhol ma'lum bo'ladi.
 */
export function usePositionDuties(positionId: string | null) {
  return useDuties(positionId)
}

/** Sinov muddatidagi lavozim standarti — formada muddatni oldindan to'ldiradi. */
export function usePositionDefaults(positionId: string | null) {
  const positions = useCatalogList<PositionItem>('positions', true)

  return useMemo(() => {
    if (positionId === null) return null
    return (positions.data ?? []).find((item) => item.id === positionId) ?? null
  }, [positions.data, positionId])
}
