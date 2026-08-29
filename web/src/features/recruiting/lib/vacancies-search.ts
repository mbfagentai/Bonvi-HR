import { VACANCY_STATUSES, type VacancyStatus } from '../api/recruiting-api'

/**
 * Vakansiyalar ro'yxatining holati — manzil satrida
 * (`employments-search.ts` naqshi, sabab ham bir xil).
 *
 * NEGA komponent ichidagi `useState` emas: HR «ochiq vakansiyalar»ni
 * filtrlab, biror qatorga bosib nomzodlar ro'yxatiga o'tadi va
 * «orqaga» bosganda o'sha filtrga qaytishi kerak. Manzilda saqlangan
 * holat brauzer tarixi bilan qaytadi, sahifa yangilanganda saqlanadi va
 * havola qilib bo'lishiladi.
 *
 * Barcha maydonlar IXTIYORIY va standart qiymat manzilga YOZILMAYDI.
 */
export interface VacanciesSearch {
  /** 1 dan boshlanadi. 1 — standart, manzilda ko'rinmaydi. */
  page?: number
  /**
   * Holat — server sanoq NOMINI kutadi («Open», «Closed»).
   *
   * NEGA manzilda ham aynan shu nom: backend hujjatida so'rov satri
   * shunday yozilgan (`?status=Open`) va o'rtada yana bir xarita
   * qo'yish ikkita nomlash tizimi hosil qilardi
   * (`recruiting-api.ts` boshidagi izoh).
   *
   * Eski havolalardagi `?status=Paused` shu yerda JIMGINA tashlanadi
   * (`readStatus`) va ro'yxat filtrsiz ochiladi — serverga yetib borsa
   * 400 bo'lardi va HR «yuklab bo'lmadi» xatosini ko'rardi. Manzildan
   * o'qishda hech qachon xato tashlanmaydi degan qoida shu faylning
   * boshida yozilgan.
   */
  status?: VacancyStatus
  /**
   * E'lon nomi.
   *
   * Manzilda qisqa nom — `vacancyTitleId` juda uzun bo'lardi. Ilgari bu
   * yerda `position`, `department` va `entity` ham bor edi; ular OLIB
   * TASHLANDI, chunki vakansiyada bunday maydonlar umuman yo'q
   * (backend'dagi `Vacancy` domen izohi).
   */
  title?: string
  sort?: VacanciesSortParam
}

/**
 * Server tanidigan saralash kalitlari — AYNAN shu ikkitasi
 * (`RecruitingQueryParsing.ParseVacancySort`). Ilgari ikkinchisi
 * `position` edi; vakansiyada lavozim qolmagach, saralanadigan yagona
 * matnli ustun e'lon nomi bo'lib qoldi.
 *
 * Noma'lum kalit serverda jimgina sukutga tushadi, lekin manzilga uni
 * yozib qo'yishning ma'nosi yo'q: havola bilan bo'lishilganda ikkinchi
 * odam boshqacha tartib ko'rardi va sababini tushunmasdi.
 */
export type VacanciesSortKey = 'openedAt' | 'title'

/** «title» — o'sish, «-openedAt» — kamayish tartibida. */
export type VacanciesSortParam = VacanciesSortKey | `-${VacanciesSortKey}`

/**
 * Standart saralash: ochilgan sana bo'yicha YANGIDAN ESKIGA.
 *
 * NEGA: vakansiya — «hozir nima izlanyapti» degan savol. Eng yangi
 * ochilgan o'rin tepada turishi kerak, alifbo esa bu yerda hech qanday
 * savolga javob bermaydi.
 */
export const DEFAULT_SORT: VacanciesSortParam = '-openedAt'

/** Toolbar ishlaydigan ko'rinish: bo'sh satr — «filtr qo'yilmagan». */
export interface VacanciesFilterState {
  status: VacancyStatus | ''
  vacancyTitleId: string
}

export const DEFAULT_FILTERS: VacanciesFilterState = {
  status: '',
  vacancyTitleId: '',
}

/**
 * Manzil satridan kelgan qiymatlarni tekshiradi.
 *
 * Hech qachon xato TASHLAMAYDI: noto'g'ri qiymat («?page=abc»)
 * shunchaki tashlab yuboriladi va sahifa ochilaveradi — qat'iy
 * konvensiya, «oq ekran» bo'lmasligi kerak.
 */
export function validateVacanciesSearch(search: Record<string, unknown>): VacanciesSearch {
  return {
    page: readPage(search['page']),
    status: readStatus(search['status']),
    title: readId(search['title']),
    sort: normalizeSort(search['sort']),
  }
}

export function searchToFilters(search: VacanciesSearch): VacanciesFilterState {
  return {
    status: search.status ?? '',
    vacancyTitleId: search.title ?? '',
  }
}

/**
 * Foydalanuvchi biror filtr qo'yganmi.
 *
 * Sahifa va saralash hisobga OLINMAYDI: ular ro'yxatni qisqartirmaydi,
 * ya'ni bo'sh natijaning sababi bo'lolmaydi.
 */
export function isFiltered(search: VacanciesSearch): boolean {
  return search.status !== undefined || search.title !== undefined
}

/**
 * Toolbar → manzil. Bo'sh maydon `undefined` bo'lib qaytadi va manzildan
 * BUTUNLAY yo'qoladi — aks holda tozalangan filtr `?title=` bo'lib
 * osilib qolardi.
 */
export function filtersToSearch(
  filters: VacanciesFilterState,
): Omit<VacanciesSearch, 'page' | 'sort'> {
  return {
    status: filters.status === '' ? undefined : filters.status,
    title: emptyToUndefined(filters.vacancyTitleId),
  }
}

/**
 * Noma'lum qiymatdan saralash parametri (`employees-search.ts` dagi
 * `normalizeSort` bilan bir xil qoida).
 *
 * Ikki holatda `undefined` qaytadi va ikkalasi ham «standart saralash»
 * degani: kalitni server tanimaydi, yoki kalit aynan standart.
 *
 * Manzildan o'qishda ham, jadval sarlavhasi bosilganda ham SHU funksiya
 * ishlaydi: qoida bitta joyda turadi.
 */
export function normalizeSort(value: unknown): VacanciesSortParam | undefined {
  if (typeof value !== 'string') return undefined

  const desc = value.startsWith('-')
  const key = desc ? value.slice(1) : value

  if (!isSortKey(key)) return undefined

  const param: VacanciesSortParam = desc ? `-${key}` : key
  return param === DEFAULT_SORT ? undefined : param
}

/* ─────────────────────────────────────────────────────────────── o'qish */

/*
 * Ruxsat etilgan qiymatlar massiv emas, KALITLI obyekt — yangi kalit
 * qo'shilganda TypeScript shu yerni ham to'ldirishga majbur qiladi
 * (`employees-search.ts` dagi bilan bir xil sabab).
 */
const SORT_KEYS: Record<VacanciesSortKey, true> = {
  openedAt: true,
  title: true,
}

function isSortKey(value: string): value is VacanciesSortKey {
  return Object.hasOwn(SORT_KEYS, value)
}

const STATUSES: ReadonlySet<string> = new Set(VACANCY_STATUSES)

function readStatus(value: unknown): VacancyStatus | undefined {
  return typeof value === 'string' && STATUSES.has(value)
    ? (value as VacancyStatus)
    : undefined
}

function readPage(value: unknown): number | undefined {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 2) return undefined
  return value
}

/*
 * Ma'lumotnoma identifikatorlari — UUID. Shaklga tushmagan qiymat shu
 * yerda tashlanadi: u to'g'ridan-to'g'ri so'rovga tushsa server 400
 * qaytarardi va HR «yuklab bo'lmadi» xatosini ko'rardi.
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function readId(value: unknown): string | undefined {
  return typeof value === 'string' && UUID.test(value) ? value : undefined
}

function emptyToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}
