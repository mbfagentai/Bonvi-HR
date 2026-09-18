import { todayIso } from '@/shared/lib/today'

import {
  DASHBOARD_STATUSES,
  MAX_REPORT_DAYS,
  REPORT_KINDS,
  type ReportKind,
} from './attendance-presentation'
import type {
  DashboardSort,
  DashboardStatus,
  PassDirection,
  PassFilters,
  PassMethod,
} from '../api/attendance-api'
import { PASS_DIRECTIONS, PASS_METHODS } from '../api/attendance-api'

/**
 * Verifikatsiya ekranlarining manzil (URL) holati.
 *
 * Bu fayldagi tiplar server DTO si EMAS — manzil satri va ekran filtrlari.
 * Shu sababli ular generatsiya qilinmaydi: `./bonvi gen` faqat server
 * shartnomasini beradi. Serverga ketadigan shakl (`PassFilters`) esa
 * generatsiya qilingan parametrlardan olinadi (`attendance-api.ts`).
 *
 * NEGA komponent ichidagi `useState` emas: sahifalar marshrut
 * komponentlari va ular boshqa bo'limga o'tilganda BUTUNLAY unmount
 * bo'ladi — holat ichkarida bo'lsa, «orqaga» bosgan HR tanlagan oyidan
 * va tabidan ayrilardi (`employees-search.ts` dagi bilan bir xil sabab).
 * Manzildagi holat esa brauzer tarixi bilan qaytadi, yangilashda
 * saqlanadi va havola qilib bo'lishiladi.
 *
 * To'rtta ekranning manzili shu yerda:
 *   · STATISTIKA — kun, qidiruv, holat, filtrlar, saralash va sahifa;
 *   · TABEL — oy manzilda (§8.2: «month + list search»);
 *   · HISOBOTLAR — tab manzilda (§8.2: «kind + list search»);
 *   · O'TISHLAR — qidiruv matni VA `passId` (pastdagi izoh).
 * O'tishlar jurnalining sana oralig'i, qurilma va yo'nalish filtrlari
 * esa sahifaning O'ZIDA qoladi — `hodisalar.tsx` da allaqachon qabul
 * qilingan yechim. Pastdagi `PassFilterState` aynan o'sha sahifa holati
 * uchun, manzil uchun emas.
 */

/* ═════════════════════════════════════════════════ STATISTIKA (manzil) */

/** Sahifa hajmlari — «50 / 157» menyusidagi uchta tanlov. */
export const DASHBOARD_PAGE_SIZES = [25, 50, 100] as const
export type DashboardPageSize = (typeof DASHBOARD_PAGE_SIZES)[number]

/** Sukut — 50 (shartnoma). Manzilda ko'rinmaydi. */
export const DEFAULT_DASHBOARD_PAGE_SIZE: DashboardPageSize = 50

/**
 * Davomat statistikasining manzili. HAMMASI manzilda (loyiha konvensiyasi):
 * «orqaga» oldingi kunga va filtrga qaytaradi, havola aynan shu ko'rinishni
 * ochadi. Standart qiymatlar YOZILMAYDI.
 *
 * Filtr nomlari xodimlar ro'yxatidagi bilan bir xil (`entity`,
 * `department`, `position` — `employees-search.ts`): ikki ekran orasida
 * havola qo'lda tuzatilsa ham bir xil o'qiladi.
 */
export interface DashboardSearch {
  /**
   * Kun, `yyyy-MM-dd`. Yo'q — BUGUN.
   *
   * ⭐ Bugun manzilga ATAYLAB yozilmaydi (tabeldagi oydan farqli): bu sahifa
   * kun bo'yi ochiq turadi va yarim tundan keyin o'zi yangi kunga o'tishi
   * kerak. Manzilda qotib qolgan sana esa jonli yangilanishni kechagi
   * kunda ushlab qolardi.
   */
  date?: string
  q?: string
  /** Diagramma bo'lagi yoki legenda bosilganda tanlangan holat. */
  status?: DashboardStatus
  entity?: string
  department?: string
  position?: string
  /** Faqat `-name` yoziladi — A→Z sukut. */
  sort?: DashboardSort
  page?: number
  /** Faqat 25 yoki 100 yoziladi — 50 sukut. */
  size?: DashboardPageSize
}

export function validateDashboardSearch(search: Record<string, unknown>): DashboardSearch {
  return {
    date: readIsoDate(search['date']),
    q: readText(search['q']),
    status: readDashboardStatus(search['status']),
    entity: readId(search['entity']),
    department: readId(search['department']),
    position: readId(search['position']),
    sort: search['sort'] === '-name' ? '-name' : undefined,
    page: readPage(search['page']),
    size: readPageSize(search['size']),
  }
}

/**
 * «Eslab qolinadigan» qism — foydalanuvchining ish uslubi: qaysi firma,
 * bo'lim va lavozim, qanday tartib va sahifada nechta qator.
 *
 * Kun, qidiruv, holat va sahifa raqami ATAYLAB yo'q: ular bugungi bir
 * martalik savol. Ertaga ochilgan sahifa kechagi qidiruv matni yoki
 * «Kechikkanlar» bo'lagi bilan ochilsa, HR ro'yxat nega qisqa ekanini
 * tushunmay qolardi.
 */
export type DashboardPin = Pick<DashboardSearch, 'entity' | 'department' | 'position' | 'sort' | 'size'>

export const DASHBOARD_PIN_FIELDS = ['entity', 'department', 'position', 'sort', 'size'] as const

/** Manzildan yoki saqlangan nusxadan eslab qolinadigan qismni ajratadi. */
export function dashboardPinOf(search: DashboardSearch): DashboardPin {
  return {
    entity: search.entity,
    department: search.department,
    position: search.position,
    sort: search.sort,
    size: search.size,
  }
}

/** Ikki nusxa bir xilmi (`undefined` va yo'q maydon — teng). */
export function samePin(left: DashboardPin, right: DashboardPin): boolean {
  return DASHBOARD_PIN_FIELDS.every((field) => left[field] === right[field])
}

/** Hech bo'lmasa bitta maydon qo'yilganmi. */
export function hasPinValues(pin: DashboardPin): boolean {
  return DASHBOARD_PIN_FIELDS.some((field) => pin[field] !== undefined)
}

/**
 * Kunni surish: «2026-09-17», −1 → «2026-09-16».
 *
 * UTC yarim tuniga bog'lab hisoblanadi (`monthRange` dagi sabab): mahalliy
 * konstruktor yozgi vaqtli mintaqada bir kunni 23 yoki 25 soat qilib,
 * sanani ikki marta bir joyda ushlab qolishi mumkin edi.
 */
export function shiftIsoDate(value: string, delta: number): string {
  const parsed = new Date(`${value}T00:00:00Z`)
  if (Number.isNaN(parsed.getTime())) return value

  parsed.setUTCDate(parsed.getUTCDate() + delta)
  return `${parsed.getUTCFullYear()}-${pad(parsed.getUTCMonth() + 1)}-${pad(parsed.getUTCDate())}`
}

/* ═══════════════════════════════════════════════════════ O'TISHLAR (manzil) */

/**
 * O'tishlar jurnalining manzili: `q` va `passId`.
 *
 * ⚠️ NEGA umumiy `validateListSearch` EMAS (§8.2 da shunday yozilgan
 * edi): 2026-09-17 tuzatishi bilan taxtadagi «ishdan ketgan xodim»
 * ogohlantirishi har bir qatorni `/verifikatsiya/otishlar?passId=…` ga
 * bog'laydi (§8.4). `validateListSearch` faqat `q` ni qoldiradi va
 * `passId` ni JIMGINA tashlab yuborardi — havola ochilar edi-yu,
 * xavfsizlik hodisasining o'zi ko'rinmasdi.
 */
export interface PassesSearch {
  q?: string
  /** Ajratib ko'rsatiladigan o'tish (UUID). Noto'g'ri shakl — tashlanadi. */
  passId?: string
}

export function validatePassesSearch(search: Record<string, unknown>): PassesSearch {
  return {
    q: readText(search['q']),
    passId: readId(search['passId']),
  }
}

/* ══════════════════════════════════════════════════════════════════ TABEL */

export interface TimesheetSearch {
  /** 1 dan boshlanadi. 1 — standart, manzilda ko'rinmaydi. */
  page?: number
  /** Qidiruv matni: xodim F.I.Sh. */
  q?: string
  /**
   * Oy — `year` va `month` DOIM birga. Ikkovi ham yo'q bo'lsa: joriy oy.
   *
   * ⭐ ISTISNO: standart qiymat manzilga YOZILMASLIGI qoidasi bu yerda
   * qo'llanmaydi. «Joriy oy» — vaqtga bog'liq standart, konstanta emas:
   * sentabrda yuborilgan havola oktabrda ochilsa, oy yozilmagan bo'lsa
   * BOSHQA tabelni ko'rsatardi. Foydalanuvchi oyni tanlagan zahoti u
   * manzilga tushadi va havola ma'nosini o'zgartirmaydi.
   */
  year?: number
  month?: number
}

/**
 * Manzil satridan kelgan qiymatlarni tekshiradi.
 *
 * Hech qachon xato TASHLAMAYDI: `?month=99` yoki `?page=abc` ni istalgan
 * odam yozishi mumkin (yoki eski havola shunday qolgan bo'ladi). Bunda
 * ekran «oq sahifa» bo'lib qolmasligi kerak — noto'g'ri qiymat tashlab
 * yuboriladi va standart ishlaydi (loyihaning qat'iy konvensiyasi).
 */
export function validateTimesheetSearch(search: Record<string, unknown>): TimesheetSearch {
  const year = readYear(search['year'])
  const month = readMonth(search['month'])

  /*
    Oy YARIM holatda qabul qilinmaydi: faqat `?year=2026` yoki faqat
    `?month=3` hech qanday tabelni ko'rsatmaydi. Ikkalasi ham bo'lmasa —
    joriy oy, ya'ni ekran baribir ishlaydi.
  */
  const complete = year !== undefined && month !== undefined

  return {
    page: readPage(search['page']),
    q: readText(search['q']),
    year: complete ? year : undefined,
    month: complete ? month : undefined,
  }
}

export interface MonthValue {
  year: number
  month: number
}

/** Manzildagi oy yoki — u yo'q bo'lsa — joriy oy (Toshkent kalendari). */
export function timesheetMonth(search: TimesheetSearch): MonthValue {
  return search.year !== undefined && search.month !== undefined
    ? { year: search.year, month: search.month }
    : currentMonth()
}

/**
 * Joriy oy — TOSHKENT kuni bo'yicha (`todayIso`).
 *
 * ⚠️ `new Date().getMonth()` ATAYLAB ishlatilmaydi: u BRAUZER
 * mintaqasini oladi va oyning birinchi kuni yarim tundan keyin boshqa
 * mintaqadagi mashinada hali «o'tgan oy» bo'lib turardi. Tizimda
 * «bugun» bitta va u Toshkent kuni (`timezone.ts` dagi qoida).
 */
export function currentMonth(): MonthValue {
  const [year, month] = todayIso().split('-')
  return { year: Number(year), month: Number(month) }
}

/** Oldingi/keyingi oy — oy tanlagichning strelkalari uchun. */
export function shiftMonth(value: MonthValue, delta: number): MonthValue {
  const zeroBased = value.month - 1 + delta

  return {
    year: value.year + Math.floor(zeroBased / 12),
    // `%` manfiy son uchun manfiy qoldiq beradi — 12 qo'shib normallanadi
    month: ((zeroBased % 12) + 12) % 12 + 1,
  }
}

/** Oyning birinchi va oxirgi kuni: `{ from: '2026-09-01', to: '2026-09-30' }`. */
export function monthRange(value: MonthValue): { from: string; to: string } {
  /*
    Oxirgi kun UTC da hisoblanadi (`Date.UTC(year, month, 0)` — keyingi
    oyning «nol-kuni», ya'ni shu oyning oxirgi kuni). Mahalliy
    konstruktor brauzer mintaqasiga tayanardi va yozgi vaqt qo'llanadigan
    mintaqalarda 30 ni 29 qilib ko'rsatishi mumkin edi.
  */
  const lastDay = new Date(Date.UTC(value.year, value.month, 0)).getUTCDate()
  const month = pad(value.month)

  return {
    from: `${value.year}-${month}-01`,
    to: `${value.year}-${month}-${pad(lastDay)}`,
  }
}

/* ═══════════════════════════════════════════════════════════ HISOBOTLAR */

export interface ReportsSearch {
  page?: number
  q?: string
  /**
   * Ochiq tab — beshtadan biri (SPEC §8.4). Manzilda turadi, ya'ni
   * «kechikkanlar ro'yxati» ni havola qilib yuborish mumkin.
   */
  kind?: ReportKind
}

/**
 * Standart tab — «Kechikkanlar».
 *
 * NEGA aynan u: bu kundalik savol. «Xulosa» esa oy oxirida bir marta
 * ochiladi va uni standart qilib qo'yish har kuni ortiqcha bir bosish
 * qo'shardi. Standart bo'lgani uchun manzilda KO'RINMAYDI.
 */
export const DEFAULT_REPORT_KIND: ReportKind = 'Late'

export function validateReportsSearch(search: Record<string, unknown>): ReportsSearch {
  return {
    page: readPage(search['page']),
    q: readText(search['q']),
    kind: readKind(search['kind']),
  }
}

export function reportKind(search: ReportsSearch): ReportKind {
  return search.kind ?? DEFAULT_REPORT_KIND
}

/** Tabni manzilga yozish: standart tab yozilmaydi (`?kind=Late` — shovqin). */
export function kindToSearch(kind: ReportKind): ReportKind | undefined {
  return kind === DEFAULT_REPORT_KIND ? undefined : kind
}

/* ═══════════════════════════════════════════════════════════ DAVR (from/to) */

/**
 * Davr uzunligi, kunlarda — IKKI TOMONDAN YOPIQ
 * (`ListIncidents` konvensiyasi, SPEC §6): 01-dan 01-gacha = 1 kun.
 *
 * Sana satrlari `YYYY-MM-DD` shaklida keladi va UTC yarim tuniga
 * bog'lanadi — ikkala tomon ham bir xil bog'langani uchun ayirma
 * mintaqadan mustaqil.
 */
export function rangeDays(from: string, to: string): number {
  const start = Date.parse(from)
  const end = Date.parse(to)
  if (Number.isNaN(start) || Number.isNaN(end)) return 0

  return Math.trunc((end - start) / 86_400_000) + 1
}

/**
 * Davr chegaradan uzunmi (`davomat.davr_uzun`, 400).
 *
 * NEGA klientda ham tekshiriladi: serverning 400 javobini kutib,
 * keyin uni tarjima qilib ko'rsatishdan ko'ra, tugma bosilishidan OLDIN
 * aytish tushunarliroq. Server tekshiruvi baribir joyida qoladi — bu
 * qulaylik, himoya emas.
 */
export function isRangeTooLong(from: string, to: string): boolean {
  return rangeDays(from, to) > MAX_REPORT_DAYS
}

/** `from > to` — `davomat.davr_notogri` (400). */
export function isRangeInverted(from: string, to: string): boolean {
  return from !== '' && to !== '' && from > to
}

/* ═════════════════════════════════════════════════ O'TISHLAR JURNALI (holat) */

/**
 * Jurnal filtrlarining ekran holati — bo'sh satr «filtr qo'yilmagan»
 * degani (`EmployeesFilterState` bilan bir xil kelishuv).
 *
 * ⚠️ Manzilda TURMAYDI (fayl boshidagi izoh): u yerda faqat `q` bor.
 */
export interface PassFilterState {
  from: string
  to: string
  deviceId: string
  direction: PassDirection | ''
  method: PassMethod | ''
  /**
   * Moslik holati: `''` — hammasi, `matched` — bog'langanlar,
   * `unmatched` — bog'lanmaganlar.
   *
   * NEGA satr, `boolean | undefined` emas: `<Select>` ning qiymati satr
   * bo'ladi va uch holatni bitta maydonda ifodalashning eng oddiy yo'li
   * shu. So'rovga aylantirishda `matched: true | false | undefined`
   * bo'lib chiqadi (`passFiltersToQuery`).
   */
  matched: '' | 'matched' | 'unmatched'
  legalEntityId: string
  departmentId: string
}

export const DEFAULT_PASS_FILTERS: PassFilterState = {
  from: '',
  to: '',
  deviceId: '',
  direction: '',
  method: '',
  matched: '',
  legalEntityId: '',
  departmentId: '',
}

/**
 * Ekran holati → so'rov parametrlari.
 *
 * Bo'sh maydon `undefined` bo'lib qaytadi va so'rovga umuman tushmaydi
 * (`attendance-api.ts` dagi `withoutBlanks`). Bitta ISTISNO — `matched: false`:
 * u «moslanmaganlarni ko'rsat» degan HAQIQIY qiymat va yuborilishi
 * shart.
 */
export function passFiltersToQuery(state: PassFilterState, search: string): PassFilters {
  return {
    from: emptyToUndefined(state.from),
    to: emptyToUndefined(state.to),
    deviceId: emptyToUndefined(state.deviceId),
    direction: state.direction === '' ? undefined : state.direction,
    method: state.method === '' ? undefined : state.method,
    matched: state.matched === '' ? undefined : state.matched === 'matched',
    legalEntityId: emptyToUndefined(state.legalEntityId),
    departmentId: emptyToUndefined(state.departmentId),
    search: emptyToUndefined(search.trim()),
  }
}

/**
 * Foydalanuvchi biror filtr qo'yganmi.
 *
 * Bo'sh natija matni shunga qarab tanlanadi: «hech narsa topilmadi»
 * (filtr bor) va «hali birorta o'tish kelmagan» (filtr yo'q) — mutlaqo
 * boshqa xabarlar, ikkinchisi esa qurilmalar bo'limiga yo'naltiradi
 * (SPEC §8.5).
 */
export function hasPassFilters(state: PassFilterState, search: string): boolean {
  return (
    search.trim() !== '' ||
    Object.values(state).some((value) => value !== '')
  )
}

/* ─────────────────────────────────────────────────────────────── o'qish */

/*
 * Ruxsat etilgan qiymatlar massiv emas, KALITLI obyekt bo'lishi kerak
 * degan qoida (`employees-search.ts`) bu yerda qo'llanmadi: sanoq
 * ro'yxatlari ALLAQACHON `attendance-api.ts` da bitta manba sifatida
 * turibdi va ikkinchi nusxasi aynan o'sha «jimgina eskirish» xavfini
 * tug'dirardi. Tekshiruv shu ro'yxatlarning o'zidan o'qiydi.
 */

function readKind(value: unknown): ReportKind | undefined {
  if (typeof value !== 'string') return undefined
  const found = REPORT_KINDS.find((kind) => kind === value)
  // Standart tab manzilda saqlanmaydi — natija aynan bir xil
  return found === undefined || found === DEFAULT_REPORT_KIND ? undefined : found
}

/** Filtr qiymatini manzildan o'qish — jurnal havolasi qo'lda yozilsa ham ishlasin. */
export function readDirection(value: unknown): PassDirection | undefined {
  return typeof value === 'string'
    ? PASS_DIRECTIONS.find((item) => item === value)
    : undefined
}

export function readMethod(value: unknown): PassMethod | undefined {
  return typeof value === 'string' ? PASS_METHODS.find((item) => item === value) : undefined
}

/**
 * `yyyy-MM-dd` — faqat HAQIQIY kalendar sanasi. «2026-02-31» shaklga mos,
 * lekin `Date` uni jimgina 3-martga aylantirardi: qaytarib solishtirib,
 * bunday sana tashlanadi.
 */
export function readIsoDate(value: unknown): string | undefined {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/u.test(value)) return undefined

  const parsed = new Date(`${value}T00:00:00Z`)
  if (Number.isNaN(parsed.getTime())) return undefined

  const roundTrip = `${parsed.getUTCFullYear()}-${pad(parsed.getUTCMonth() + 1)}-${pad(parsed.getUTCDate())}`
  return roundTrip === value ? value : undefined
}

export function readDashboardStatus(value: unknown): DashboardStatus | undefined {
  return typeof value === 'string'
    ? DASHBOARD_STATUSES.find((status) => status === value)
    : undefined
}

/** 25 yoki 100. Sukut (50) va boshqa son manzilga yozilmaydi — `undefined`. */
export function readPageSize(value: unknown): DashboardPageSize | undefined {
  const found = DASHBOARD_PAGE_SIZES.find((size) => size === value)
  return found === undefined || found === DEFAULT_DASHBOARD_PAGE_SIZE ? undefined : found
}

/**
 * Sahifa raqami.
 *
 * 1 dan kichigi ham, kasr ham, `NaN` ham qabul qilinmaydi — barchasi
 * birinchi sahifaga tushadi. Yuqori chegara bu yerda tekshirilmaydi:
 * oxirgi sahifa nechanchiligi `total` kelmaguncha noma'lum.
 */
function readPage(value: unknown): number | undefined {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 2) return undefined
  return value
}

/**
 * Qidiruv matni QIRQILMAYDI — faqat butunlay bo'sh qiymat tashlanadi.
 *
 * Qirqilsa «Karimov » deb yozilgandagi probel 350 ms dan keyin manzildan
 * qaytib kelib maydondan yo'qolardi — foydalanuvchi hali familiyasini
 * yozib ulgurmasidan (`employees-search.ts` dagi bilan bir xil).
 */
function readText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined
}

function readYear(value: unknown): number | undefined {
  /*
    Chegara ataylab keng: tizimda 1990-yillardagi mehnat yozuvlari ham
    bor va tabel kelajak oyga ham ochilishi mumkin (jadval oldindan
    tuzilgan bo'lsa). Maqsad — ma'noli yilni tanlash emas, `?year=0` va
    `?year=99999` kabi qiymatlarni so'rovga o'tkazmaslik.
  */
  if (typeof value !== 'number' || !Number.isInteger(value)) return undefined
  return value >= 1990 && value <= 2100 ? value : undefined
}

function readMonth(value: unknown): number | undefined {
  if (typeof value !== 'number' || !Number.isInteger(value)) return undefined
  return value >= 1 && value <= 12 ? value : undefined
}

/*
 * Identifikator — UUID. Shaklga tushmagan qiymat so'rovga yetib borsa
 * server 400 qaytarardi; bu yerda tashlab yuborish arzonroq
 * (`employees-search.ts` dagi `readId` bilan bir xil qoida).
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu

export function readId(value: unknown): string | undefined {
  return typeof value === 'string' && UUID.test(value) ? value : undefined
}

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

function emptyToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}
