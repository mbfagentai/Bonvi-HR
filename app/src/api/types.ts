/**
 * Server bilan shartnoma — `SPEC-xodim-ilovasi.md` §E.
 *
 * Nomlar backend enum'larining AYNAN o'zi (`PassDirection`,
 * `AttendanceDayStatus`, `AbsenceRequestStatus`) — HR ilovasi uchun
 * generatsiya qilingan tiplar bilan bir xil, lekin ular O'QILIB,
 * qo'lda ko'chirilgan: bu ilova HR ilovasidan hech narsa import
 * qilmaydi (UX §0.1).
 *
 * ⚠️ Haqiqiy mijoz kelganda o'zgaradigan yagona joy — `client.ts`.
 * Ekranlar shu tiplarni ko'radi va mock bilan haqiqiy server orasidagi
 * farqni sezmaydi.
 */

/** O'tishning yo'nalishi. */
export type PassDirection = 'In' | 'Out' | 'Unknown'

/** Hisoblangan kunning holati. */
export type AttendanceDayStatus =
  | 'Present'
  | 'Absent'
  | 'DayOff'
  | 'DayOffWorked'
  | 'MissingExit'
  | 'MissingEntry'
  | 'NoSchedule'
  | 'NotEmployed'
  | 'Excused'
  | 'Planned'

/** Yo'qlik so'rovining holati. */
export type AbsenceRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'

/**
 * Belgi qayerdan kelgan. Ekranda `Turniket` / `Mobil ilova` bo'lib
 * ko'rinadi — matn tarjima faylida, simda esa kod.
 */
export type MarkSource = 'Terminal' | 'Mobile'

export interface AbsenceTypeDto {
  id: string
  name: string
}

/**
 * `GET /api/me`.
 *
 * Yo'qlik turlari ATAYLAB shu javob ichida (SPEC §E): ilova HR
 * endpoint'iga murojaat qila olmaydi, ro'yxat esa kichik va kamdan-kam
 * o'zgaradi.
 */
export interface MeDto {
  employeeId: string
  fullName: string
  /** To'liq manzil yoki `null` — rasmi yo'q xodim odatiy hol. */
  photoUrl: string | null
  departmentName: string | null
  positionName: string | null
  /** «Telefondan belgilash» bayrog'i (SPEC §C), sukut bo'yicha `false`. */
  mobileCheckInAllowed: boolean
  absenceTypes: AbsenceTypeDto[]
}

/** Kun ichidagi bitta o'tish (SPEC §E — GAP-1 yopilgan). */
export interface AttendanceMarkDto {
  /** UTC lahza. Ekranda Toshkent vaqtiga o'giriladi. */
  occurredAt: string
  direction: PassDirection
  source: MarkSource
  /** Faqat mobil belgi uchun to'ladi. */
  siteName: string | null
}

/** Tabelning bitta katagi — HR ko'radigan katakning aynan o'zi. */
export interface AttendanceDayDto {
  /** «2026-09-22» — Toshkent sanasi. */
  date: string
  status: AttendanceDayStatus
  workedMinutes: number
  lateMinutes: number
  earlyLeaveMinutes: number
  breakDeductionMinutes: number
  /** Birinchi kirish (UTC). */
  firstInAt: string | null
  /** Oxirgi chiqish (UTC). */
  lastOutAt: string | null
  /** Kun REJASINING boshlanishi (UTC) — jadval bo'yicha, kelish emas. */
  planStartAt: string | null
  /** Reja tugashi (UTC) — tungi smenada ertangi kunga tushadi. */
  planEndAt: string | null
  marks: AttendanceMarkDto[]
}

export interface AttendanceMonthDto {
  year: number
  month: number
  days: AttendanceDayDto[]
}

export interface CheckInRequest {
  direction: PassDirection
  latitude: number
  longitude: number
  accuracyMeters: number
}

/**
 * ⚠️ Javobdagi `occurredAt` — SERVER vaqti, telefonniki emas
 * (SPEC §B3). Tasdiq qatoridagi soat aynan shu yerdan olinadi.
 */
export interface CheckInResult {
  occurredAt: string
  direction: PassDirection
  workSiteName: string
}

export interface AbsenceRequestDto {
  id: string
  absenceTypeId: string
  absenceTypeName: string
  /** Davr boshi (Toshkent sanasi), shu kun ham kiradi. */
  fromDate: string
  /** Davr oxiri, shu kun ham kiradi. */
  toDate: string
  /** Faqat bir kunlik so'rovda to'ladi: «09:00». */
  fromTime: string | null
  toTime: string | null
  note: string | null
  status: AbsenceRequestStatus
  decisionNote: string | null
  decidedByName: string | null
  decidedAt: string | null
  createdAt: string
}

export interface CreateAbsenceRequest {
  absenceTypeId: string
  fromDate: string
  toDate: string
  fromTime: string | null
  toTime: string | null
  note: string | null
}

export interface LoginRequest {
  /** Telefon raqami — HR bergan login. */
  login: string
  password: string
}

export interface LoginResult {
  accessToken: string
  refreshToken: string
  /** `true` bo'lsa ilova parol ekranidan boshqa joyga o'tkazmaydi. */
  mustChangePassword: boolean
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}
