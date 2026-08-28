/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN. `./bonvi gen` bilan almashtiriladi.
 *
 * Xodimlar moduli bilan bir xil sabab (`employees-api.ts` boshidagi izohga
 * qarang): OpenAPI hujjatida bu endpoint'lar hali chiqmagan. Tiplar taxmin
 * emas — ular jonli API javobidan va backend'ning domen qatlamidan olingan:
 *   api/src/Bonvi.Domain/Employees/Probation.cs
 *   api/src/Bonvi.Application/Employees/Abstractions/IEmployeeReadModel.cs
 */
import { api } from '@/shared/api/http-client'

/**
 * Sinov muddati natijasi.
 *
 * Qiymatlar domendagi `ProbationStatus` bilan AYNAN bir xil — server javobda
 * son qaytaradi, so'rovda esa nom qabul qiladi (pastdagi izohga qarang).
 */
export const PROBATION_STATUSES = {
  inProgress: 1,
  passed: 2,
  failed: 3,
  leftEarly: 4,
  cancelled: 5,
} as const

export type ProbationStatus = keyof typeof PROBATION_STATUSES

/** Yakuniy natijalar — «davom etmoqda» ni server rad etadi. */
export const PROBATION_RESULTS: readonly ProbationStatus[] = [
  'passed',
  'failed',
  'leftEarly',
  'cancelled',
]

/** Ro'yxatdagi bitta sinov muddati — xodim ma'lumoti bilan birga. */
export interface ProbationItem {
  id: string
  employmentId: string
  employeeId: string
  employeeName: string
  /** Rasm kaliti — `EmployeePhoto` shu bo'yicha rasmni oladi. */
  photoKey: string | null
  department: string | null
  position: string | null
  /** Identifikator — «doimiyga o'tkazish» formasi tanlovni shu bilan to'ldiradi. */
  departmentId: string | null
  positionId: string | null
  hireDate: string | null
  startDate: string | null
  endDate: string | null
  status: ProbationStatus
  resolvedAt: string | null
  conclusion: string | null
  /** Muddat tugashiga qolgan kun. Manfiy — muddat o'tib ketgan. */
  daysLeft: number
  /** Server hisoblagan ogohlantirish belgisi (7 kun qoldi yoki o'tdi). */
  needsWarning: boolean
}

export interface ProbationFilters {
  status: ProbationStatus | ''
  /**
   * Xodim bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon.
   * Bo'sh satr — «qidiruvsiz» (server ham shunday tushunadi).
   */
  search: string
}

/**
 * Sinov xulosasi — va u «o'tdi» bo'lsa, doimiy xodimga o'tkazish shartlari.
 *
 * Tayinlash maydonlari IXTIYORIY va `null` qoldirilgani «o'zgarmadi»
 * degani: forma avvalgi qiymatlarni oldindan to'ldiradi va HR faqat
 * o'zgargan qatorga tegadi. Server ularni faqat `passed` bilan qabul
 * qiladi, aks holda `sinov.tayinlash_otmagan` (400) qaytadi.
 */
export interface ResolveProbationRequest {
  status: ProbationStatus
  conclusion: string | null
  positionId?: string | null
  departmentId?: string | null
  salaryAmount?: number | null
  rate?: number | null
  orderNo?: string | null
  orderDate?: string | null
}

/**
 * Holati «sinovda», lekin sinov yozuvi YO'Q munosabat.
 *
 * Bunday xodim xodimlar ro'yxatida ham ko'rinmaydi (u yerda doimiylar),
 * sinov muddatlari ro'yxatida ham (u yerda sinov YOZUVLARI) — ya'ni
 * tizimda bor, ekranda yo'q. Manba: Excel'dan ko'chirish, «91.5»
 * varag'ida bo'lmagan odamlar.
 */
export interface MissingProbationItem {
  employmentId: string
  employeeId: string
  employeeName: string
  photoKey: string | null
  legalEntity: string | null
  department: string | null
  position: string | null
  hireDate: string | null
  /** Bugungi kunga qancha ishlagani — HR qarorini shu raqamga qarab beradi. */
  workedDays: number
}

/** Ommaviy tasdiqlash natijasi. */
export interface ConfirmMissingResult {
  confirmed: number
  /** Ro'yxatda qolmagani — xato emas, boshqa birov ulgurgan bo'lishi mumkin. */
  skipped: number
}

export interface ExtendProbationRequest {
  newEndDate: string
}

/* ─────────────────────────────────────────────────────────── so'rovlar */

/**
 * Sinov muddatlari ro'yxati.
 *
 * Server sahifalamaydi va bu ataylab: bir vaqtda sinovda bo'lgan xodimlar
 * o'nlab, ko'pi bilan bir necha yuz. Ularni bitta ro'yxatda ko'rish HR uchun
 * qulayroq — «keyingi sahifa» tugmasi bu yerda faqat xalaqit berardi.
 */
export async function fetchProbations(
  filters: ProbationFilters,
  signal?: AbortSignal,
): Promise<ProbationItem[]> {
  // Bo'sh filtr YUBORILMAYDI: `?status=` server uchun «noto'g'ri qiymat»,
  // «hammasi» emas.
  //
  // Qidiruv SERVERGA yuboriladi, klientda `filter()` qilinmaydi: server
  // ism yig'ish (қ/ғ/ҳ/ў/ё) va telefon ajratgichlarini tashlash
  // qoidalarini biladi, klient esa yo'q. Bu qoidani bu yerda takrorlash
  // ikkinchi nusxa yaratardi va u vaqt o'tib birinchisidan ajralib
  // ketardi.
  const params: Record<string, string> = {}

  if (filters.status !== '') params['status'] = statusName(filters.status)
  if (filters.search.trim() !== '') params['search'] = filters.search

  const { data } = await api.get<unknown>('/api/probations', { params, signal })

  return toList(data)
}

/** Muddati yaqinlashganlar — sahifa boshidagi ogohlantirish uchun. */
export async function fetchEndingSoon(
  withinDays: number,
  signal?: AbortSignal,
): Promise<ProbationItem[]> {
  const { data } = await api.get<unknown>('/api/probations/ending-soon', {
    params: { withinDays },
    signal,
  })

  return toList(data)
}

export async function resolveProbation(
  id: string,
  request: ResolveProbationRequest,
): Promise<void> {
  await api.post(`/api/probations/${id}/resolve`, {
    status: statusName(request.status),
    conclusion: request.conclusion,
    // `undefined` maydonni axios butunlay tashlab yuboradi, `null` esa
    // yuboriladi — ikkalasi ham server uchun «o'zgarmadi» degani, shu
    // sababli qo'shimcha tozalash kerak emas.
    positionId: request.positionId,
    departmentId: request.departmentId,
    salaryAmount: request.salaryAmount,
    rate: request.rate,
    orderNo: request.orderNo,
    orderDate: request.orderDate,
  })
}

/** Sinov yozuvisiz «sinovda» qolganlar. Server sahifalamaydi. */
export async function fetchMissingProbations(
  signal?: AbortSignal,
): Promise<MissingProbationItem[]> {
  const { data } = await api.get<unknown>('/api/probations/missing', { signal })

  return Array.isArray(data) ? data.map(toMissing) : []
}

/** Belgilanganlarni doimiy holatga o'tkazadi. */
export async function confirmMissingProbations(
  employmentIds: readonly string[],
): Promise<ConfirmMissingResult> {
  const { data } = await api.post<unknown>('/api/probations/missing/confirm', {
    employmentIds,
  })

  const box = (data ?? {}) as Record<string, unknown>

  return {
    confirmed: typeof box['confirmed'] === 'number' ? box['confirmed'] : 0,
    skipped: typeof box['skipped'] === 'number' ? box['skipped'] : 0,
  }
}

export async function extendProbation(
  id: string,
  request: ExtendProbationRequest,
): Promise<void> {
  await api.post(`/api/probations/${id}/extend`, request)
}

/* ─────────────────────────────────────────────────── xaritalash (map) */

/**
 * Enum ikki xil ko'rinishda yuradi va buni bilib turish kerak.
 *
 * Javobda — SON (`"status": 1`), so'rovda — NOM (`{"status":"Passed"}`).
 * Sabab: JSON serializatori enum'ni son qilib yozadi, model bog'lovchi esa
 * nomni ham, sonni ham tushunadi. Nom yuborish aniqroq: so'rov logida
 * `"Passed"` ko'rinadi, `2` emas.
 */
function statusName(status: ProbationStatus): string {
  return status.charAt(0).toUpperCase() + status.slice(1)
}

function toStatus(value: unknown): ProbationStatus {
  for (const [name, code] of Object.entries(PROBATION_STATUSES)) {
    if (value === code || value === name) return name as ProbationStatus
    if (typeof value === 'string' && value.toLowerCase() === name.toLowerCase()) {
      return name as ProbationStatus
    }
  }

  // Noma'lum qiymat «davom etmoqda» deb qaraladi: yozuv ro'yxatdan
  // yo'qolgandan ko'ra, holati noaniq bo'lib ko'ringani yaxshiroq.
  return 'inProgress'
}

function toList(data: unknown): ProbationItem[] {
  return Array.isArray(data) ? data.map(toItem) : []
}

function toItem(raw: unknown): ProbationItem {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employmentId: text(box['employmentId']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    employeeName: text(box['employeeName']) ?? '—',
    photoKey: text(box['photoKey']),
    department: text(box['department']),
    position: text(box['position']),
    departmentId: text(box['departmentId']),
    positionId: text(box['positionId']),
    hireDate: text(box['hireDate']),
    startDate: text(box['startDate']),
    endDate: text(box['endDate']),
    status: toStatus(box['status']),
    resolvedAt: text(box['resolvedAt']),
    conclusion: text(box['conclusion']),
    daysLeft: typeof box['daysLeft'] === 'number' ? box['daysLeft'] : 0,
    needsWarning: box['needsWarning'] === true,
  }
}

function toMissing(raw: unknown): MissingProbationItem {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    employmentId: text(box['employmentId']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    employeeName: text(box['employeeName']) ?? '—',
    photoKey: text(box['photoKey']),
    legalEntity: text(box['legalEntity']),
    department: text(box['department']),
    position: text(box['position']),
    hireDate: text(box['hireDate']),
    workedDays: typeof box['workedDays'] === 'number' ? box['workedDays'] : 0,
  }
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}
