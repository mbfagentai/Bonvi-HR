/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN.
 *
 * Endpoint'lar va tiplar `http://localhost:7080/openapi/v1.json` dagi
 * haqiqiy shartnomaga qarab yozilgan, taxmin bilan emas. Lekin bu fayl
 * generatsiya qilingan emas — u qo'lda yangilanishi kerak.
 *
 * Shu sababli:
 *   1. `./bonvi gen` — OpenAPI'dan haqiqiy klient generatsiya qilinadi
 *      (`@/shared/api/generated/catalog/catalog` va `.../model`)
 *   2. Shu fayl O'CHIRILADI, `catalog-queries.ts` generatsiya qilingan
 *      hook'larga o'tkaziladi
 *   3. Bu yerdagi interfeyslar o'rniga generatsiya qilingan DTO'lar ishlatiladi
 *
 * Bu yerdagi tiplar serverning to'liq DTO'sining KERAKLI QISMI:
 * `LegalEntityDto.taxId`, `DirectionDto.color`, `PenaltyTypeDto.severity`
 * kabi turga xos maydonlar hozircha interfeysda ko'rsatilmaydi va
 * saqlashda yuborilmaydi (server ularni ixtiyoriy deb qabul qiladi).
 */
import { api } from '@/shared/api/http-client'

/** Bir xil tuzilishga ega — bitta komponent bilan ko'rsatiladigan ma'lumotnomalar. */
export const SIMPLE_CATALOG_KINDS = [
  'legal-entities',
  'directions',
  'termination-reasons',
  'award-types',
  'penalty-types',
  'document-types',
  /*
    Yo'qlik turlari — davomat so'rovlari shu ro'yxatdan tanlanadi
    (SPEC-so'rovlar §B1). Tuzilishi bo'yicha yuqoridagilar bilan aynan
    bir xil (faqat nom), shu sababli u ham umumiy `CatalogPage` bilan
    chiziladi.
  */
  'absence-types',
  /*
    Where candidates heard about the job (docs/SPEC-qayerdan-bilgan.md §3).
    Same shape as absence types: a name plus one behaviour flag.
  */
  'referral-channels',
] as const

export type SimpleCatalogKind = (typeof SIMPLE_CATALOG_KINDS)[number]

/** Bo'limlar (daraxt) va lavozimlar (vazifalar bilan) — o'z sahifasi bor. */
export type CatalogKind = SimpleCatalogKind | 'departments' | 'positions'

/** Barcha ma'lumotnomalarga umumiy maydonlar. */
export interface CatalogItem {
  /** Yozuvning yagona kaliti — barcha bog'lanishlar shu UUID orqali. */
  id: string
  name: string
  isActive: boolean
  note: string | null
  /**
   * Faqat yo'qlik turlarida: tasdiqlangan so'rov vaqti ishlangan vaqt
   * hisoblanadimi (SPEC-so'rovlar §B3 AMENDED 2026-10-17).
   */
  countsAsWorkTime?: boolean
  /**
   * Faqat yo'qlik turlarida: xodim ilovadan o'zi so'ray oladimi
   * (SPEC-jadval-sorovi §A). `false` — turni faqat HR kiritadi.
   */
  employeeCanRequest?: boolean
  /**
   * Referral channels only: choosing this channel asks the candidate for a
   * free-text detail («Tanishlar orqali» → who recommended them).
   */
  asksDetail?: boolean
}

/**
 * Serverdagi `SaveReferenceCommand` — sakkizta ma'lumotnomaga YAGONA buyruq.
 * Turga xos maydonlar ixtiyoriy: yuborilmasa, o'zgarmaydi.
 */
export interface CatalogItemInput {
  name: string
  note?: string | null
  /** Faqat yo'qlik turi uchun yuboriladi — boshqalarida server e'tiborsiz qoldiradi. */
  countsAsWorkTime?: boolean
  /** Faqat yo'qlik turi uchun — `countsAsWorkTime` bilan bir xil qoida. */
  employeeCanRequest?: boolean
  /** Referral channels only — sent with every save of that kind, ignored by the others. */
  asksDetail?: boolean
}

export interface DepartmentItem extends CatalogItem {
  parentId: string | null
  /** Ierarxiyadagi chuqurlik: 0 — eng yuqori. */
  level: number
  /** To'liq yo'l: «7-Departament / Metan zavod / Plazma bo'limi». */
  path: string | null
}

/** Daraxt tugunlari `children` orqali bog'lanadi (serverda yig'iladi). */
export interface DepartmentNode extends DepartmentItem {
  children: DepartmentNode[]
}

export interface DepartmentInput extends CatalogItemInput {
  /** Server buni FAQAT yaratishda qabul qiladi; keyin — `move` amali. */
  parentId?: string | null
}

export interface PositionItem extends CatalogItem {
  departmentId: string | null
  /** Daraja: 1 — ishchi … 5 — rahbariyat. */
  grade: number
  /** Standart sinov muddati, kunda (0–180). */
  probationDays: number | null
  /**
   * Vazifalar soni.
   *
   * ⚠️ Serverning `PositionDto` sida bu maydon HOZIRCHA YO'Q — ro'yxatda
   * u «—» bo'lib chiqadi. Backend qo'shgach o'zi to'ldiriladi; har bir
   * qator uchun alohida so'rov yuborish (N+1) qabul qilinmadi.
   */
  dutyCount?: number | null
}

export interface PositionInput extends CatalogItemInput {
  departmentId?: string | null
  grade?: number | null
  probationDays?: number | null
}

export interface PositionDuty {
  id: string
  text: string
  sortOrder: number
}

/** Lavozimning to'liq ko'rinishi — vazifalari bilan (`PositionDetailsDto`). */
export interface PositionDetails extends PositionItem {
  duties: PositionDuty[]
}

const base = (kind: CatalogKind): string => `/api/catalog/${kind}`

/**
 * Ro'yxat SERVERDAN ALLAQACHON TARTIBLANGAN holda keladi — nom bo'yicha,
 * o'zbek ICU kolatsiyasi bilan. Klientda qayta saralanmaydi: `Array.sort`
 * brauzer kolatsiyasiga tayanadi va «Ғ», «Қ», «Ҳ» harflarini serverdan
 * boshqacha joyga qo'yib, ikki xil tartib ko'rsatib qo'yardi.
 */
export async function fetchCatalog<T extends CatalogItem = CatalogItem>(
  kind: CatalogKind,
  onlyActive: boolean,
  signal?: AbortSignal,
): Promise<T[]> {
  const { data } = await api.get<T[]>(base(kind), {
    params: { onlyActive },
    signal,
  })
  return data
}

export async function fetchCatalogItem<T extends CatalogItem = CatalogItem>(
  kind: CatalogKind,
  id: string,
  signal?: AbortSignal,
): Promise<T> {
  const { data } = await api.get<T>(`${base(kind)}/${id}`, { signal })
  return data
}

export async function createCatalogItem<
  T extends CatalogItem = CatalogItem,
  TInput extends CatalogItemInput = CatalogItemInput,
>(kind: CatalogKind, body: TInput): Promise<T> {
  const { data } = await api.post<T>(base(kind), body)
  return data
}

export async function updateCatalogItem<
  T extends CatalogItem = CatalogItem,
  TInput extends CatalogItemInput = CatalogItemInput,
>(kind: CatalogKind, id: string, body: TInput): Promise<T> {
  const { data } = await api.put<T>(`${base(kind)}/${id}`, body)
  return data
}

/**
 * Yozuv o'chirilmaydi — faolsizlantiriladi.
 * HR ma'lumoti tarixiy hujjat: eski buyruqlarda uchragan lavozim nomi
 * ro'yxatdan yo'qolsa, o'sha buyruq ma'nosini yo'qotadi.
 */
export async function setCatalogItemActive(
  kind: CatalogKind,
  id: string,
  active: boolean,
): Promise<void> {
  await api.post(`${base(kind)}/${id}/${active ? 'activate' : 'deactivate'}`)
}

/* ---------------------------------------------------------------- bo'limlar */

export async function fetchDepartmentTree(
  onlyActive: boolean,
  signal?: AbortSignal,
): Promise<DepartmentNode[]> {
  const { data } = await api.get<DepartmentNode[]>('/api/catalog/departments/tree', {
    params: { onlyActive },
    signal,
  })
  return data
}

/**
 * Bo'limni boshqa ota bo'lim ostiga ko'chiradi.
 * `newParentId: null` — ildizga chiqaradi.
 * Halqa hosil bo'lsa server 409 va `bolim.halqa` kodini qaytaradi.
 */
export async function moveDepartment(
  id: string,
  newParentId: string | null,
): Promise<void> {
  await api.post(`/api/catalog/departments/${id}/move`, { newParentId })
}

/* --------------------------------------------------------------- lavozimlar */

/**
 * Vazifa amallari — hammasi YANGILANGAN TO'LIQ RO'YXATNI qaytaradi.
 * Shu sababli javobni keshga to'g'ridan-to'g'ri yozish mumkin va
 * qo'shimcha `GET` so'rovi kerak emas.
 */
export async function fetchDuties(
  positionId: string,
  signal?: AbortSignal,
): Promise<PositionDuty[]> {
  const { data } = await api.get<PositionDuty[]>(
    `/api/catalog/positions/${positionId}/duties`,
    { signal },
  )
  return data
}

export async function addDuty(
  positionId: string,
  text: string,
): Promise<PositionDuty[]> {
  const { data } = await api.post<PositionDuty[]>(
    `/api/catalog/positions/${positionId}/duties`,
    { text },
  )
  return data
}

export async function removeDuty(
  positionId: string,
  dutyId: string,
): Promise<PositionDuty[]> {
  const { data } = await api.delete<PositionDuty[]>(
    `/api/catalog/positions/${positionId}/duties/${dutyId}`,
  )
  return data
}

/** Butun tartib bir so'rovda yuboriladi — har siljishda alohida emas. */
export async function reorderDuties(
  positionId: string,
  orderedIds: string[],
): Promise<PositionDuty[]> {
  const { data } = await api.put<PositionDuty[]>(
    `/api/catalog/positions/${positionId}/duties/order`,
    { orderedIds },
  )
  return data
}
