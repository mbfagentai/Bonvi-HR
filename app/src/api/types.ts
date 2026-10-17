/**
 * Server bilan shartnoma — `SPEC-xodim-ilovasi.md` §E.
 *
 * ⚠️ Bu fayl SIMDAGI shaklning AYNAN nusxasi: maydon nomlari, ichma-ich
 * tuzilish va qiymatlar backend DTO'lari bilan bir xil (`MeDto`,
 * `MyAttendanceDto`, `TimesheetCellDto`, `AbsenceRequestDto`,
 * `PagedResult`, `EmployeeAuthResultDto`, `MobileCheckInResultDto`).
 * Ular O'QILIB, qo'lda ko'chirilgan: bu ilova HR ilovasidan hech narsa
 * import qilmaydi (UX §0.1). Ekranga qulay shakl — `data/` da, bu yerda
 * emas: aks holda mock bilan server orasidagi farq yana jimgina o'sardi.
 *
 * Lahzalar — ISO satr (server UTC `…+00:00` beradi); ekranda Toshkent
 * vaqtiga `lib/format` o'giradi. Sanalar — `2026-09-22`. Kun ichidagi
 * vaqt — `08:30:00` (`TimeOnly`).
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

/** `MyAbsenceTypeDto`. */
export interface AbsenceTypeDto {
  id: string
  name: string
}

/**
 * `GET /api/me` — `MeDto`.
 *
 * Yo'qlik turlari ATAYLAB shu javob ichida (SPEC §E): ilova HR
 * endpoint'iga murojaat qila olmaydi, ro'yxat esa kichik va kamdan-kam
 * o'zgaradi.
 */
export interface MeDto {
  employeeId: string
  fullName: string
  /** «Aliyev S.B.» */
  shortName: string
  /**
   * Rasm SAQLASH KALITI, manzil emas; `null` — rasm yo'q. Rasmning o'zi
   * `GET /api/me/photo` dan tokenga bog'langan holda olinadi, kalit esa
   * keshni yangilash belgisi sifatida ishlatiladi.
   */
  photoKey: string | null
  department: string | null
  position: string | null
  /** «Telefondan belgilash» bayrog'i (SPEC §C), sukut bo'yicha `false`. */
  mobileCheckInAllowed: boolean
  absenceTypes: AbsenceTypeDto[]
}

/** Kun ichidagi bitta belgi — `MyMarkDto`. */
export interface AttendanceMarkDto {
  /** Lahza (UTC). Ekranda Toshkent vaqtiga o'giriladi. */
  at: string
  direction: PassDirection
  source: MarkSource
  /** Faqat telefon belgisida to'ladi. */
  siteName: string | null
}

/** Tabelning bitta katagi — HR ko'radigan katakning AYNAN o'zi (`TimesheetCellDto`). */
export interface TimesheetCellDto {
  /** «2026-09-22» — Toshkent sanasi. */
  date: string
  status: AttendanceDayStatus
  workedMinutes: number
  lateMinutes: number
  earlyLeaveMinutes: number
  /** Birinchi kirish (UTC). */
  firstInAt: string | null
  /** Oxirgi chiqish (UTC) — tungi smenada ertangi kunga tushadi. */
  lastOutAt: string | null
  breakTakenMinutes: number
  breakDeductionMinutes: number
  /** Kun REJASINING boshlanishi (UTC) — jadval bo'yicha, kelish emas. */
  planStartAt: string | null
  /** Reja tugashi (UTC) — tungi smenada ertangi kunga tushadi. */
  planEndAt: string | null
  /** Shu katakdagi telefon belgilari soni. */
  mobileMarks: number
  /**
   * `workedMinutes` ning tasdiqlangan «ish vaqti» so'rovidan kelgan qismi
   * (masalan boshqa zavodga ish bilan borilgan vaqt). Eski server — yo'q.
   */
  creditedMinutes?: number
}

/**
 * Oyning bitta kuni — `MyAttendanceDayDto`: katak ICHMA-ICH (`day`) va
 * shu KATAKKA tushgan belgilar (tabel qoidasi bo'yicha: tungi smenaning
 * ertalabki chiqishi kechagi katakda).
 */
export interface MyAttendanceDayDto {
  day: TimesheetCellDto
  marks: AttendanceMarkDto[]
}

/** Oy bo'yicha jami — HR tabelidagi bilan bir xil hisob (`TimesheetTotalsDto`). */
export interface TimesheetTotalsDto {
  workedMinutes: number
  plannedMinutes: number
  presentDays: number
  absentDays: number
  lateMinutes: number
  earlyLeaveMinutes: number
}

/**
 * «Hozir» — belgilash kartasi uchun (`MyCurrentStateDto`). So'ralgan oydan
 * qat'i nazar HOZIRGI lahza uchun hisoblanadi.
 */
export interface MyCurrentStateDto {
  /** Joriy smena tegishli bo'lgan katak (ochiq tungi smenada — kechagi kun). */
  date: string
  /** Tugma yuboradigan yo'nalish. */
  nextDirection: PassDirection
  /** Oxirgi belgi (18 soatlik oyna ichida). */
  lastMark: AttendanceMarkDto | null
}

/** `GET /api/me/attendance?year&month` — `MyAttendanceDto`. */
export interface AttendanceMonthDto {
  year: number
  month: number
  /** Tabelda qator bo'lmasa (xodim bu oyda hali ishlamagan) — bo'sh. */
  days: MyAttendanceDayDto[]
  totals: TimesheetTotalsDto
  current: MyCurrentStateDto
}

/** `POST /api/me/check-in` tanasi. */
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

/** `AbsenceRequestDto` — HR ro'yxatidagi yozuvning o'zi. */
export interface AbsenceRequestDto {
  id: string
  employeeId: string
  employeeName: string
  employeePhotoKey: string | null
  positionName: string | null
  departmentName: string | null
  absenceTypeId: string
  absenceTypeName: string
  /** Davr boshi (Toshkent sanasi), shu kun ham kiradi. */
  fromDate: string
  /** Davr oxiri, shu kun ham kiradi. */
  toDate: string
  /** Faqat bir kunlik so'rovda to'ladi: «09:00:00». */
  fromTime: string | null
  toTime: string | null
  note: string | null
  status: AbsenceRequestStatus
  decisionNote: string | null
  decidedByName: string | null
  decidedAt: string | null
  createdAt: string
  /** So'rovni xodimning o'zi ilovadan yuborgan. */
  submittedByEmployee: boolean
  /** So'rovni xodimning o'zi ilovadan qaytarib olgan. */
  cancelledByEmployee: boolean
}

/**
 * Jadval so'rovining turi (SPEC-jadval-sorovi §C1): `Change` — «Kunni
 * o'zgartirish», `Swap` — «Kun almashtirish». Simda — enum NOMI.
 */
export type ScheduleChangeKind = 'Change' | 'Swap'

/**
 * Jadval so'rovining bitta kuni — `ScheduleChangeDayDto`.
 *
 * Almashtirishda (`Swap`) ham har bir sana alohida qator: server yaratish
 * paytida ikkala kunning amaldagi rejasini olib, ularni o'rin almashtirib
 * yozadi. `swapWith` — juftining sanasi, faqat ko'rsatish uchun.
 */
export interface ScheduleChangeDayDto {
  date: string
  isWorking: boolean
  /** «08:30:00»; dam olish kunida `null`. */
  startTime: string | null
  /** Boshlanishdan oldin bo'lsa — tungi smena, ertasi kuni tugaydi. */
  endTime: string | null
  /** Tanaffus — ikkalasi birga yoki ikkalasi ham `null`. */
  breakStart: string | null
  breakEnd: string | null
  swapWith: string | null
  /**
   * Ayiriladigan tanaffus, daqiqa — oynadan farq qilishi mumkin: jadvaldan
   * olingan kunda 60 daqiqalik tushlik 12:00–15:00 oynasida.
   */
  breakMinutes: number
}

/** `ScheduleChangeRequestDto` — HR navbatidagi yozuvning o'zi. */
export interface ScheduleChangeRequestDto {
  id: string
  employeeId: string
  employeeName: string
  employeePhotoKey: string | null
  positionName: string | null
  departmentName: string | null
  kind: ScheduleChangeKind
  /** Sana tartibida. */
  days: ScheduleChangeDayDto[]
  note: string | null
  /** Yo'qlik so'rovidagi to'rtta holatning aynan o'zi. */
  status: AbsenceRequestStatus
  decisionNote: string | null
  decidedByName: string | null
  decidedAt: string | null
  createdAt: string
  submittedByEmployee: boolean
  cancelledByEmployee: boolean
}

/** `POST /api/me/schedule-requests` dagi bitta kun — vaqt `HH:mm:ss`. */
export interface ScheduleChangeDayInput {
  date: string
  isWorking: boolean
  startTime: string | null
  endTime: string | null
  breakStart: string | null
  breakEnd: string | null
  /** Tanaffus uzunligi, daqiqa; `null` — server oyna uzunligini oladi. */
  breakMinutes: number | null
}

/** Kun almashtirish jufti: `date` ⇄ `withDate`. */
export interface ScheduleSwapInput {
  date: string
  withDate: string
}

/**
 * `POST /api/me/schedule-requests` tanasi. `Change` da faqat `days`,
 * `Swap` da faqat `swaps` to'ladi — ikkinchisi bo'sh ro'yxat.
 */
export interface CreateScheduleChangeRequest {
  kind: ScheduleChangeKind
  days: ScheduleChangeDayInput[]
  swaps: ScheduleSwapInput[]
  note: string | null
}

/**
 * `GET /api/me/plan?date=` — `DayPlanDto`: sananing AMALDAGI rejasi
 * (tasdiqlangan jadval so'rovi ham hisobga olingan). Jadval so'rovi
 * formasini xodimning hozirgi soatlari bilan to'ldirish uchun.
 */
export interface DayPlanDto {
  date: string
  /** `false` — shu sanada xodimga jadval biriktirilmagan. */
  hasSchedule: boolean
  isWorking: boolean
  startTime: string | null
  endTime: string | null
  breakStart: string | null
  breakEnd: string | null
  /**
   * Ayiriladigan tanaffus, daqiqa — jadvalda oynadan alohida. Forma uni
   * tanaffusi o'zgartirilmagan kun bilan qaytaradi, aks holda server oyna
   * uzunligini olardi (12:00–15:00 da 60 emas, 180).
   */
  breakMinutes: number
}

/** Sahifalangan ro'yxat — `PagedResult<T>`. */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
  pageCount: number
}

/** `POST /api/me/requests` tanasi — vaqt `HH:mm:ss` (`TimeOnly`). */
export interface CreateAbsenceRequest {
  absenceTypeId: string
  fromDate: string
  toDate: string
  fromTime: string | null
  toTime: string | null
  note: string | null
}

export interface LoginRequest {
  /** Telefon raqami — xodim profilidagi asosiy raqam. */
  login: string
  /** HR bergan parol (odatda 6 raqam). Xodim uni almashtirmaydi. */
  password: string
}

/** Token juftligi — `EmployeeAuthResultDto`. Kirish va yangilash shuni qaytaradi. */
export interface LoginResult {
  accessToken: string
  /** Access token muddati. */
  expiresAt: string
  refreshToken: string
}
