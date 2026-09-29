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
   * Nom bo'yicha qidiruv matni.
   *
   * Nomi `q` — nomzodlar va boshqa ro'yxatlardagi bilan bir xil
   * (`candidates-search.ts`, `shared/lib/list-search.ts`): havolani qo'lda
   * yozgan odam har ekranda bitta nomni kutadi.
   *
   * Ilgari bu yerda `title` turardi — «Vakansiya nomlari» ma'lumotnomasidagi
   * yozuvning identifikatori. Ma'lumotnoma olib tashlandi (nom endi
   * vakansiyaning o'z matni), eski xatcho'plardagi `?title=…` esa
   * jimgina tashlanadi: u endi hech narsani bildirmaydi.
   */
  q?: string
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
  search: string
  status: VacancyStatus | ''
}

export const DEFAULT_FILTERS: VacanciesFilterState = {
  search: '',
  status: '',
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
    q: readText(search['q']),
    sort: normalizeSort(search['sort']),
  }
}

export function searchToFilters(search: VacanciesSearch): VacanciesFilterState {
  return {
    search: search.q ?? '',
    status: search.status ?? '',
  }
}

/**
 * Foydalanuvchi biror filtr qo'yganmi.
 *
 * Sahifa va saralash hisobga OLINMAYDI: ular ro'yxatni qisqartirmaydi,
 * ya'ni bo'sh natijaning sababi bo'lolmaydi.
 */
export function isFiltered(search: VacanciesSearch): boolean {
  return search.status !== undefined || search.q !== undefined
}

/**
 * Toolbar → manzil. Bo'sh maydon `undefined` bo'lib qaytadi va manzildan
 * BUTUNLAY yo'qoladi — aks holda tozalangan qidiruv `?q=` bo'lib
 * osilib qolardi.
 */
export function filtersToSearch(
  filters: VacanciesFilterState,
): Omit<VacanciesSearch, 'page' | 'sort'> {
  return {
    status: filters.status === '' ? undefined : filters.status,
    q: readText(filters.search),
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

/**
 * Qidiruv matni QIRQILMAYDI — faqat butunlay bo'sh qiymat tashlanadi
 * (`candidates-search.ts` dagi `readText` bilan bir xil sabab: qirqilsa,
 * «Sotuvchi » dagi probel 350 ms dan keyin manzildan qaytib kelib
 * maydondan yo'qolardi — HR keyingi so'zni yozib ulgurmasidan).
 */
function readText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined
}
