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
import type { SelectOption } from '@/shared/ui'

import {
  addAdditionalDuty,
  changeSalary,
  closeFlag,
  createFlag,
  deleteEmployeePhoto,
  fetchEmployee,
  fetchEmployeePhoto,
  fetchEmployees,
  fetchFlags,
  fetchTimeline,
  hire,
  terminate,
  transfer,
  updateEmployee,
  uploadEmployeePhoto,
  type AdditionalDutyRequest,
  type ChangeSalaryRequest,
  type CreateFlagRequest,
  type EmployeeListFilters,
  type HireRequest,
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
import { saveBlobAs } from '../lib/document-file'

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
   * Rasm keshi ATAYLAB `employees` ildizidan tashqarida turadi.
   *
   * Aks holda har bir ishga qabul yoki belgi qo'shish butun ildizni bekor
   * qilib, sahifadagi 25 ta rasmni ham qaytadan yuklatardi. Rasm esa
   * xodim yozuvidan mustaqil: u faqat `photoKey` o'zgarganda o'zgaradi.
   */
  photos: (id: string) => ['employee-photo', id] as const,
  photo: (id: string, photoKey: string | null) =>
    ['employee-photo', id, photoKey] as const,
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
export function useEmployeePhoto(employeeId: string, photoKey: string | null) {
  return useQuery({
    queryKey: employeeKeys.photo(employeeId, photoKey),
    queryFn: ({ signal }) => fetchEmployeePhoto(employeeId, signal),
    /**
     * Kaliti yo'q — rasmi ham yo'q, so'rov umuman yuborilmaydi.
     * Busiz 25 qatorli sahifa har ochilganda 25 ta keraksiz 404 ketardi.
     */
    enabled: employeeId !== '' && photoKey !== null,
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

/**
 * Ishga qabul.
 *
 * Muvaffaqiyatdan keyin butun `employees` ildizi bekor qilinadi: yangi
 * xodim ro'yxatda, sinovdagilar sanog'ida va (qayta ishga olish bo'lsa)
 * o'sha shaxs kartasida bir vaqtda paydo bo'lishi kerak.
 */
export function useHire() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: HireRequest) => hire(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })
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

export function useCreateFlag(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: CreateFlagRequest) => createFlag(employeeId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: employeeKeys.flags(employeeId) })
      // Ro'yxatdagi ogohlantirish belgisi ham shu ma'lumotdan chiziladi
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })
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
export function useDownloadDocument(employeeId: string) {
  return useMutation({
    mutationFn: (doc: EmployeeDocument) =>
      fetchDocumentFile(employeeId, doc.id).then((blob) => ({ blob, doc })),
    onSuccess: ({ blob, doc }) => saveBlobAs(blob, doc.fileName),
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
