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
  /**
   * `true` — tasdiqlangan so'rov vaqti ish vaqti hisoblanadi («Ishxona ishi
   * bilan»); `false` — shaxsiy tur: vaqtli so'rov soatlari ishlangan vaqtdan
   * ayriladi (SPEC-sorov-tahriri §5). Eski server — yo'q (forma izoh chiqarmaydi).
   */
  countsAsWorkTime?: boolean
}

/**
 * Jadval so'rovidagi tanaffus qoidasi (SPEC-sorov-tahriri §2.2) — tizim
 * sozlamasidagi to'rt son. Vaqtlar `TimeOnly` («12:00:00»).
 */
export interface RequestBreakRuleDto {
  /** Ruxsat etilgan oraliq boshi. */
  from: string
  /** Ruxsat etilgan oraliq oxiri. */
  to: string
  /** Tanaffus uzunligi (dushanba–payshanba, shanba, yakshanba); jumada — eng kami. */
  minutes: number
  /** Jumada eng ko'pi. */
  fridayMaxMinutes: number
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
  /**
   * Jadval so'rovidagi tanaffus qoidasi — forma xodimni serverga borishdan
   * oldin ogohlantiradi. Eski server — yo'q: tekshiruv faqat serverda.
   */
  requestBreakRule?: RequestBreakRuleDto | null
  /**
   * Xodim kamida bitta bo'limning boshlig'i (SPEC-jamoam §C5) — «Jamoa»
   * tabi faqat shunda ko'rinadi. Eski server — yo'q (tab ham yo'q).
   */
  isTeamLead?: boolean
  /** Xodimning «Rahbar»i (SPEC-jamoam §D); `null` — topilmadi. Eski server — yo'q. */
  manager?: MyManagerDto | null
}

/** `MeDto.manager` — faqat ism: ilova rahbarning kartasini ochmaydi. */
export interface MyManagerDto {
  fullName: string
}

/**
 * Jamoa panelidagi odamning shu kundagi holati — HR paneli
 * (`AttendanceDashboardStatus`) bilan AYNAN bir xil to'plam. Simda — enum NOMI.
 */
export type TeamBoardStatus =
  | 'OnTime'
  | 'Late'
  | 'Absent'
  | 'NotStarted'
  | 'DayOff'
  | 'NoSchedule'
  | 'Excused'
  | 'Arrived'

/** Boshliq qamrovidagi bitta bo'lim — `GET /api/me/team` (SPEC-jamoam §C1). */
export interface TeamDepartmentDto {
  id: string
  name: string
  /** «Ishlab chiqarish / 1-smena» — to'liq yo'l. */
  path: string | null
  /** Ierarxiyadagi chuqurlik: 0 — eng yuqori. */
  level: number
  parentId: string | null
}

/** `GET /api/me/team` — qamrov, daraxt tartibida tekislangan. */
export interface TeamScopeDto {
  departments: TeamDepartmentDto[]
}

/** Holatlar bo'yicha sanoq — qidiruv va holat filtridan OLDINGI to'plam (`total` bilan bir xil). */
export interface TeamBoardCountsDto {
  onTime: number
  late: number
  absent: number
  notStarted: number
  dayOff: number
  noSchedule: number
  excused: number
  arrived: number
}

/**
 * Paneldagi bitta xodim. ⚠️ Faqat davomat va telefon: rasm KALITI, JSHSHIR,
 * pasport, oklad va boshqa HR maydonlari bu yerga ATAYLAB kelmaydi (§C2).
 */
export interface TeamBoardItemDto {
  employeeId: string
  fullName: string
  /** Rasmning o'zi `GET /api/me/team/members/{id}/photo` dan. */
  hasPhoto: boolean
  positionName: string | null
  departmentName: string | null
  /** Kunning birinchi kirishi (UTC); `null` — o'tish yo'q. */
  arrivedAt: string | null
  /** Oxirgi chiqish (UTC); `null` — chiqish yo'q. */
  leftAt: string | null
  status: TeamBoardStatus
  /** Faqat `Late` da: reja boshlanishidan kelgan lahzagacha daqiqalar. */
  lateMinutes: number | null
  /** `leftAt` haqiqiy chiqish emas — jadvalsiz kunning avtomatik «ketdi»si. */
  leftAuto: boolean
  /** Asosiy telefon — ilovadagi boshqa joylar kabi formatlangan; yo'q bo'lsa `null`. */
  phone: string | null
}

/** `GET /api/me/team/board` — HR panelining o'sha hisobi, boshliq qamrovida. */
export interface TeamBoardDto {
  /** Qaysi Toshkent sanasi. */
  date: string
  isToday: boolean
  /** Server holatlarni hisoblagan lahza — «yangilandi HH:mm». */
  asOf: string
  /** To'plamdagi odamlar (bo'lim filtridan keyin, qidiruv va holatdan oldin). */
  total: number
  counts: TeamBoardCountsDto
  items: TeamBoardItemDto[]
  page: number
  pageSize: number
  /** Qidiruv va holatdan keyingi qatorlar soni. */
  totalItems: number
  pageCount: number
}

/** Panel so'rovi. `date: null` — bugun (sanani server o'zi qo'yadi). */
export interface TeamBoardQuery {
  date: string | null
  departmentId: string | null
  status: TeamBoardStatus | null
  search: string
  page: number
  pageSize: number
}

/** Jamoa a'zosi — oy ekranining sarlavhasi uchun (SPEC-jamoam §C3). */
export interface TeamMemberDto {
  employeeId: string
  fullName: string
  hasPhoto: boolean
  positionName: string | null
  departmentName: string | null
  phone: string | null
}

/**
 * `GET /api/me/team/members/{id}/attendance?year&month` — `attendance`
 * `/api/me/attendance` ning AYNAN o'zi: a'zoning oyi uning o'z ilovasidagidek.
 */
export interface TeamMemberAttendanceDto {
  employee: TeamMemberDto
  attendance: AttendanceMonthDto
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
  /**
   * Kun «Erkin ish jadvali» bo'yicha hisoblangan (SPEC-erkin-jadval §2):
   * kechikish va erta ketish yo'q, kun ishlangan vaqt va «Ish vaqti»
   * bo'yicha baholanadi. Oyna faqat ma'lumot uchun — undan tashqaridagi
   * borliq ham sanaladi (egasi, 2026-10-20). Eski server — yo'q (erkin emas).
   */
  isFlexible?: boolean
  /** Kunning rejadagi daqiqalari — erkin kunda «Ish vaqti» (masalan 450). Hosil qilingan katakda 0. */
  plannedMinutes?: number
  /** «Kam ishlangan» — erkin kunda normadan kam ishlangan daqiqalar; boshqa kunda 0. */
  shortfallMinutes?: number
  /**
   * «Shaxsiy vaqt» — tasdiqlangan shaxsiy so'rovning rejaga tushgan daqiqalari
   * (SPEC-sorov-tahriri §5): sababli, lekin ish vaqti EMAS — `workedMinutes`
   * ga kirmaydi. Hosil qilingan katakda va eski serverda — 0 / yo'q.
   */
  personalMinutes?: number
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
  /** Kataklardagi «Kam ishlangan» yig'indisi. Eski server — yo'q. */
  shortfallMinutes?: number
  /** Kataklardagi «Shaxsiy vaqt» yig'indisi. Eski server — yo'q. */
  personalMinutes?: number
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
  /**
   * Yozuv versiyasi — har o'zgarishda +1 (SPEC-sorov-tahriri §3.3). Tahrirda
   * aynan ko'rsatilgani qaytariladi: boshqa joyda o'zgargan bo'lsa server
   * 409 `davomat.sorov_ozgargan` beradi. Eski server — yo'q.
   */
  version?: number
  /** Qaror kutayotganda oxirgi marta o'zgartirilgan lahza (UTC); o'zgartirilmagan — `null`. */
  editedAt?: string | null
  /** Oxirgi o'zgartirish ilovadan, xodimning o'zidan kelgan. */
  editedByEmployee?: boolean
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
  /** «Erkin ish jadvali» kuni. Eski server — yo'q (erkin emas). */
  isFlexible?: boolean
  /** «Ish vaqti», daqiqa — faqat erkin kunda, aks holda `null`. */
  requiredMinutes?: number | null
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
  /** Yo'qlik so'rovidagi bilan bir xil: versiya va oxirgi o'zgartirish. */
  version?: number
  editedAt?: string | null
  editedByEmployee?: boolean
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
  /** «Erkin ish jadvali» — faqat ish kunida `true` bo'ladi. */
  isFlexible: boolean
  /**
   * «Ish vaqti», daqiqa. `null` — server sukutni o'zi yozadi: smena −
   * tanaffus (SPEC-erkin-jadval §2.5). Erkin bo'lmagan kunda doim `null`.
   */
  requiredMinutes: number | null
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
 * `PUT /api/me/schedule-requests/{id}` tanasi — butun so'rov qayta yoziladi
 * (yaratishdagi qoidalar bilan), `version` MAJBURIY.
 */
export type UpdateScheduleChangeRequest = CreateScheduleChangeRequest & { version: number }

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
  /** Sana «Erkin ish jadvali» bo'yicha. Eski server — yo'q (erkin emas). */
  isFlexible?: boolean
  /** «Ish vaqti», daqiqa — faqat erkin kunda, aks holda `null`. */
  requiredMinutes?: number | null
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

/** `PUT /api/me/requests/{id}` tanasi — yaratishdagining o'zi va MAJBURIY `version`. */
export type UpdateAbsenceRequest = CreateAbsenceRequest & { version: number }

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
