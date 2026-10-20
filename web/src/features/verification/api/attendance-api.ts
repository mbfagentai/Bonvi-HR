/**
 * Davomat (verifikatsiya) moduli — API qatlami.
 *
 * ⭐ TIPLARNING HAMMASI GENERATSIYA QILINGAN: `./bonvi gen` →
 * `@/shared/api/generated/model` (DTO va sanoqlar) va
 * `@/shared/api/generated/attendance` (yo'l + parametr bilan chaqiruvlar).
 * Bu faylda server DTO sining qo'lda yozilgan nusxasi YO'Q — faqat:
 *   · generatsiya qilingan tiplarning taxalluslari (sahifalar tipni
 *     modul ichidan oladi, generatsiya qilingan papkani bilmaydi);
 *   · yupqa chaqiruv o'ramlari (SPEC §8.3: «hand-write only the thin call
 *     wrappers»);
 *   · shartnomada umuman YO'Q bo'lgan klient tiplari — har birining
 *     ustida nega generatsiya qilinmagani yozilgan.
 *
 * NEGA shunday (SPEC §8.3, `PLAN.md` texnik qarz 3-band): qo'lda yozilgan
 * klientlarda server maydoni o'zgarsa, klient JIMGINA eskirardi
 * (`flags`/`activeFlags` xatosi shunday chiqqan). Endi server DTO si
 * o'zgarsa — `./bonvi gen` → shu fayl va sahifalar kompilyatsiyada qizaradi.
 *
 * Farq chiqsa TUZATILADIGAN joy — server yoki shu fayldagi o'ram, hech
 * qachon generatsiya qilingan kod (u git'ga tushmaydi va har gen'da
 * qayta yoziladi).
 */
import axios from 'axios'

import {
  activateAttendanceDevice,
  activateWorkSchedule,
  approveDeviceRemovals,
  confirmAttendanceIdentityImport,
  createAttendanceIdentity,
  createScheduleAssignment,
  createWorkSchedule,
  deactivateAttendanceDevice,
  deactivateWorkSchedule,
  deleteAttendanceDevice,
  deleteAttendanceIdentity,
  deleteScheduleAssignment,
  deleteWorkSchedule,
  getAttendanceDashboard,
  getAttendanceDevice,
  getAttendanceExceptions,
  getAttendancePass,
  getAttendanceSummary,
  getAttendanceTimesheet,
  getAttendanceTrend,
  getDeviceEnrollments,
  getFormerEmployeeAlerts,
  fillWorkScheduleYear,
  cancelAbsenceRequest,
  cancelScheduleChangeRequest,
  createAbsenceRequest,
  createScheduleChangeRequest,
  decideAbsenceRequest,
  decideScheduleChangeRequest,
  getPendingAbsenceRequestCount,
  getScheduleRequestBreakRule,
  getUpcomingBirthdays,
  searchAbsenceRequests,
  searchScheduleChangeRequests,
  updateAbsenceRequest,
  updateScheduleChangeRequest,
  getWorkSchedule,
  getWorkScheduleYear,
  listAttendanceDevices,
  listAttendanceIdentities,
  listScheduleAssignments,
  listUnmatchedAttendancePersons,
  listWorkSchedules,
  recomputeAttendance,
  registerAttendanceDevice,
  resolveWorkSchedule,
  resyncDeviceEnrollments,
  rotateAttendanceDeviceToken,
  searchAttendancePasses,
  setWorkScheduleDates,
  updateAttendanceDevice,
  updateWorkSchedule,
} from '@/shared/api/generated/attendance/attendance'
import {
  AttendanceIdentityProvider,
  AttendanceProvider,
  type AttendanceDashboardStatus,
  DeviceAuthMode,
  DeviceDirectionRole,
  IdentityMatchKind,
  PassDirection,
  PassMethod,
  AbsenceRequestMode,
  AbsenceRequestStatus,
  ScheduleChangeKind,
  type AbsenceDecisionBody,
  type AbsenceRequestBody,
  type AbsenceRequestCountDto,
  type AbsenceRequestDto,
  type AttendanceBirthdayDto,
  type AttendanceDashboardCountsDto,
  type AttendanceDashboardDto,
  type AttendanceDashboardItemDto,
  type AttendanceDeviceCreatedDto,
  type AttendanceDeviceDto,
  type AttendanceDeviceHealthDto,
  type DeviceOutageDto,
  type DeviceEnrollmentPageDto,
  type DeviceEnrollmentRowDto,
  type DeviceEnrollmentSummaryDto,
  type EnrollmentResyncDto,
  type AttendanceDeviceRequest as AttendanceDeviceRequestBody,
  type AttendanceExceptionDto,
  type AttendanceIdentityDto,
  type AttendancePassDto,
  type AttendanceRecomputeAcceptedDto,
  type AttendanceSummaryDto,
  type AttendanceTimesheetDto,
  type AttendanceTrendDayDto,
  type AttendanceTrendDto,
  type ConfirmIdentityImportRequest,
  type CreateAttendanceIdentityRequest,
  type ExportAttendanceExceptionsParams,
  type ExportAttendancePassesParams,
  type ExportAttendanceSummaryParams,
  type ExportAttendanceTimesheetParams,
  type FormerEmployeeAlertDto,
  type FormerEmployeeAlertsDto,
  type GetAttendanceDashboardParams,
  type GetAttendanceTrendParams,
  type GetUpcomingBirthdaysParams,
  type SearchAbsenceRequestsParams,
  type IdentityImportFailureDto,
  type IdentityImportPreviewDto,
  type IdentityImportResultDto,
  type IdentityImportRowDto,
  type ImportCandidateDto,
  type ListAttendanceIdentitiesParams,
  type ListScheduleAssignmentsParams,
  type RecomputeAttendanceRequest,
  type RequestBreakRuleDto,
  type ScheduleAssignmentDto,
  type ScheduleDatesBody,
  type ScheduleYearDayDto,
  type ScheduleYearDto,
  type ScheduleAssignmentRequest as ScheduleAssignmentRequestBody,
  type ScheduleChangeDayDto,
  type ScheduleChangeRequestBody,
  type ScheduleChangeRequestDto,
  type ScheduleResolutionDto,
  type SearchAttendancePassesParams,
  type SearchScheduleChangeRequestsParams,
  type TimesheetCellDto,
  type TimesheetRowDto,
  type TimesheetTotalsDto,
  type UnmatchedGroupDto,
  type WorkScheduleDayDto,
  type WorkScheduleDayRequest,
  type WorkScheduleDto,
  type WorkScheduleRequest as WorkScheduleRequestBody,
} from '@/shared/api/generated/model'
import { api } from '@/shared/api/http-client'
import { fileNameFromDisposition } from '@/shared/lib/download'

/* ═══════════════════════════════════════════════ butun son — «number | string» */

/**
 * Generatsiya qilingan tipdagi `number | string` → `number`.
 *
 * NEGA kerak: .NET `JsonSerializerDefaults.Web` da
 * `NumberHandling = AllowReadingFromString` — server butun sonni SATR
 * ko'rinishida ham QABUL qiladi va OpenAPI hujjati buni `integer | string`
 * deb yozadi. Bu O'QISH qoidasi: yozishda `System.Text.Json` sonni doim
 * JSON son qilib yuboradi (`WriteAsString` yoqilmagan). Ya'ni `| string`
 * serverning javobini emas, qabul qila oladigan so'rovini tasvirlaydi.
 *
 * Tipni shundayligicha sahifalarga o'tkazish har bir `minutes > 0` va
 * `Math.round(...)` ni `Number(...)` bilan o'rashga majbur qilardi —
 * ya'ni tekshiruv foydasiz shovqinga aylanardi. Shu sababli tor tip
 * GENERATSIYA QILINGAN tipdan HOSIL QILINADI (qo'lda qayta yozilmaydi):
 * server maydon qo'shsa yoki olib tashlasa, bu tip ham birga o'zgaradi.
 *
 * ⚠️ Faqat TIP darajasida. Runtime'da umumiy «satrni songa o'gir»
 * yurgizib bo'lmaydi: `rawPersonId: "10427"` ham raqamga o'xshaydi, lekin
 * u satr bo'lib qolishi shart. Javobning haqiqatan son ekani jonli API
 * bilan tekshirilgan (`docs/PENDING_WIRING.md`, gen almashtiruvi).
 */
export type AsSent<T> = number extends T
  ? Exclude<T, string>
  : T extends readonly (infer Item)[]
    ? Array<AsSent<Item>>
    : T extends object
      ? { [Key in keyof T]: AsSent<T[Key]> }
      : T

/**
 * Javobni tor tipga o'tkazadigan YAGONA joy (`AsSent` izohidagi sabab).
 *
 * Ikki bosqichli o'tkazish bilib qilingan: `AsSent<T>` shartli tip va
 * TypeScript uni `T` bilan solishtira olmaydi. Tasdiq modul bo'ylab
 * tarqalib ketmasin deb bitta funksiyaga yig'ilgan.
 */
function asSent<T>(value: T): AsSent<T> {
  return value as unknown as AsSent<T>
}

/* ═══════════════════════════════════════════════════════════════ sanoqlar */

/*
 * Sanoqlar generatsiya qilingan `const` obyektlardan QAYTA EKSPORT
 * qilinadi — qiymat ham, tip ham shu bitta nomda. Ro'yxat (`…S`)
 * `<Select>` va manzil tekshiruvi uchun o'sha obyektdan olinadi.
 *
 * Qo'lda yozilgan ro'yxatning tuzog'i (rekrutingdagi `Reserve`: yangi
 * server qiymati ro'yxatga tushmay, jimgina zaxira qiymatga aylangan) endi
 * yo'q: server sanoqqa qiymat qo'shsa, `./bonvi gen` dan keyin har bir
 * `Record<Sanoq, …>` xaritasi kompilyatsiyada qizaradi.
 */
export {
  AbsenceRequestMode,
  AbsenceRequestStatus,
  AttendanceDashboardStatus,
  AttendanceDayStatus,
  AttendanceIdentityProvider,
  AttendanceProvider,
  DeviceAuthMode,
  DeviceDirectionRole,
  DeviceEnrollmentState,
  DeviceLinkState,
  DirectionSource,
  IdentityMatchKind,
  IdentitySource,
  PassDirection,
  PassMethod,
  ScheduleChangeKind,
  ScheduleDaySource,
  ScheduleScope,
  WorkScheduleKind,
} from '@/shared/api/generated/model'

export const ATTENDANCE_PROVIDERS: readonly AttendanceProvider[] = Object.values(AttendanceProvider)

export const ATTENDANCE_IDENTITY_PROVIDERS: readonly AttendanceIdentityProvider[] =
  Object.values(AttendanceIdentityProvider)

export const DEVICE_DIRECTION_ROLES: readonly DeviceDirectionRole[] =
  Object.values(DeviceDirectionRole)

export const DEVICE_AUTH_MODES: readonly DeviceAuthMode[] = Object.values(DeviceAuthMode)

export const PASS_DIRECTIONS: readonly PassDirection[] = Object.values(PassDirection)

export const PASS_METHODS: readonly PassMethod[] = Object.values(PassMethod)

export const IDENTITY_MATCH_KINDS: readonly IdentityMatchKind[] = Object.values(IdentityMatchKind)

/**
 * Chetlanishlar hisobotining turi — so'rov parametri (SPEC §6.6).
 *
 * ⚠️ KLIENT TIPI, generatsiya qilinmagan: server `kind` ni SATR sifatida
 * bog'laydi va noto'g'ri qiymatga `davomat.hisobot_turi_notogri` bilan
 * javob beradi (B5 qarori) — shu sababli OpenAPI hujjatida bu sanoq
 * yo'q (`kind?: string`). Qiymatlar SPEC §6.6 dagi ro'yxatning AYNAN
 * nusxasi va `Shortfall` (SPEC-erkin-jadval §5.3, `ExceptionKind.Shortfall`).
 *
 * `Missing` UCHTA holatni tanlaydi: `MissingExit`, `MissingEntry` va
 * `NoSchedule` — «bu kunni hisoblab bo'lmadi» degan ro'yxat.
 */
export const EXCEPTION_KINDS = ['Late', 'EarlyLeave', 'Shortfall', 'Absent', 'Missing'] as const
export type ExceptionKind = (typeof EXCEPTION_KINDS)[number]

/* ════════════════════════════════════════════════════════ qurilmalar (§6.2) */

/**
 * Terminal ro'yxatdagi holicha.
 *
 * ⚠️ `token` bu tipda YO'Q va hech qachon bo'lmaydi: server uni faqat
 * yaratish va yangilash javobida bir marta beradi (SPEC §3.2).
 */
export type AttendanceDevice = AsSent<AttendanceDeviceDto>

/**
 * Qurilmalar sog'lig'i — Statistika sahifasining yuqorisidagi ogohlantirishlar
 * (`GET /dashboard` javobining `deviceHealth` qismi, SPEC-agent §L).
 *
 * `outages` — agenti o'n daqiqadan beri jim yoki terminali javob bermayotgan
 * qurilmalar. Chegara SERVERDA (`AttendanceRules.AgentSilenceMinutes`),
 * klient uni qayta hisoblamaydi.
 */
export type AttendanceDeviceHealth = AsSent<AttendanceDeviceHealthDto>

/** Bitta uzilgan qurilma. */
export type DeviceOutage = AsSent<DeviceOutageDto>

/**
 * Yaratish/token yangilash javobi — `{ device, token }`, ICHMA-ICH (§6.2).
 *
 * Token HAYOTDA BIR MARTA ko'rinadi, shu sababli u alohida tip:
 * chaqiruv joyi «bu javobda sir bor» ekanini tipdan ko'radi va uni
 * keshga yozib qo'ymaydi.
 */
export type AttendanceDeviceCreated = AsSent<AttendanceDeviceCreatedDto>

/**
 * Qurilma formasi yuboradigan tana — HAR BIR maydon ochiq yoziladi.
 *
 * NEGA `Required`: generatsiya qilingan tanada maydonlar ixtiyoriy
 * (server sukutga tushadi). Forma esa ularni doim yuboradi, ayniqsa
 * `hmacSecret` — u UCH holatli (`null` — tegilmaydi, `''` — tozalanadi,
 * matn — almashtiriladi) va uni «unutib» qoldirish formada ko'rinmas
 * qaror bo'lardi.
 */
export type AttendanceDeviceRequest = Required<AttendanceDeviceRequestBody>

/* ═══════════════════════════════════════════════════════ mosliklar (§6.3) */

/** (ID makoni, qurilmadagi shaxs raqami) → xodim. `photoKey` — XODIM rasmi. */
export type AttendanceIdentity = AsSent<AttendanceIdentityDto>

/** Qo'lda bog'lash tanasi — ixtiyoriy maydonlar ham ochiq (`null`) yuboriladi. */
export type CreateIdentityRequest = Required<CreateAttendanceIdentityRequest>

/**
 * Moslanmagan o'tishlar — (ID makoni, shaxs raqami) bo'yicha (SPEC §4.2).
 * `samplePassId` — rasmi BOR eng yangi o'tish: ekran YUZNI ko'rsatadi.
 */
export type UnmatchedGroup = AsSent<UnmatchedGroupDto>

/* ══════════════════════════════════════════ Verifix ro'yxatini import (§4.4) */

/** Nomzod xodim — aniq moslikda bitta, noaniqda 2…5 ta, rasm kaliti bilan. */
export type ImportCandidate = ImportCandidateDto

export type ImportPreviewRow = AsSent<IdentityImportRowDto>

/**
 * Oldindan ko'rish — SERVERDA SAQLANMAYDI (SPEC §4.4): qarorlarni KLIENT
 * ushlaydi va faqat tasdiqlanganini qaytarib yuboradi.
 */
export type ImportPreview = AsSent<IdentityImportPreviewDto>

export type ImportConfirmRequest = ConfirmIdentityImportRequest

/** `code` — `davomat.*` xato kodi, `useErrorMessage` uni tarjima qiladi. */
export type ImportFailure = IdentityImportFailureDto

/** `skipped` — allaqachon bog'langan qatorlar; bu XATO EMAS, hisobot. */
export type ImportResult = AsSent<IdentityImportResultDto>

/* ═════════════════════════════════════════════════════════ jadvallar (§6.4) */

/**
 * Jadvalning bir kuni — FORMA holati va so'rov tanasining bir kuni.
 *
 * Generatsiya qilingan so'rov kunidan `Required` bilan olinadi: tanada
 * vaqt va oyna maydonlari ixtiyoriy (server `null` deb oladi), muharrir
 * esa yettala kunning HAR BIR maydonini ushlaydi — ya'ni «yo'q» va
 * «`null`» o'rtasida uchinchi holat bo'lmasin.
 *
 * NEGA javob kunidan (`WorkScheduleDay`) alohida: javobda server
 * hisoblagan `breakCrossesMidnight` bor, saqlanmagan formada esa u hali
 * yo'q — bitta tip bo'lsa muharrir bayroqni o'zi to'qishga majbur bo'lardi.
 *
 * Vaqtlar «HH:mm» — SONIYASIZ (`fetchSchedules` o'qishda qirqadi):
 * `<input type="time">` aynan shu shaklni kutadi. Tanaffus oynasi
 * (`breakStart`/`breakEnd`) ikkalasi BIRGA to'ldiriladi yoki ikkalasi
 * `null` (§2.4.0); ayirishning o'zi faqat serverda.
 */
export type WorkScheduleDayInput = AsSent<Required<WorkScheduleDayRequest>>

/** Serverdan kelgan kun — forma maydonlari va server hisoblagan `breakCrossesMidnight`. */
export type WorkScheduleDay = AsSent<WorkScheduleDayDto>

export type WorkSchedule = AsSent<WorkScheduleDto>

/** Ettala `dayIndex` ham AYNAN bir martadan (SPEC §6.4). */
export type WorkScheduleRequest = WorkScheduleRequestBody

/**
 * Biriktirma. `photoKey` — faqat xodim pog'onasida (§6.5 qoidasi): ro'yxat
 * odamni YUZI bilan ko'rsatadi.
 */
export type ScheduleAssignment = AsSent<ScheduleAssignmentDto>

/** Darajaga mos BITTA maydon to'ldiriladi — qolganlari ochiq `null` (§2.5). */
export type ScheduleAssignmentRequest = Required<ScheduleAssignmentRequestBody>

/**
 * «Nega bu odamda shu jadval?» (SPEC §6.4).
 *
 * Natija FAQAT ikki `null` dan o'qiladi va aynan shu tartibda:
 * `employmentId === null` — ishlamagan; aks holda `scope === null` —
 * jadval yechilmadi; qolgani — yechildi (`scheduleOutcome`).
 * `matchedDepartment*` faqat `scope === 'Department'` da to'ladi va OTA
 * bo'lim bo'lishi mumkin.
 */
export type ScheduleResolution = AsSent<ScheduleResolutionDto>

/* ═══════════════════════════════════════════════════════ o'tishlar (§6.5) */

/**
 * Bitta o'tish — qurilma NIMA DEGAN bo'lsa, o'shanday.
 *
 * Rasm kalitlari: `photoKey` — TURNIKET surati, `employeePhotoKey` —
 * xodim kartasi. `skewWarning` va `lateMinutes` — SERVER hisobi; klient
 * ularni `clockSkewSeconds` yoki «Kechikkanlar» hisobotidan qayta
 * chiqarmaydi (ikkinchi, mo'rt nusxa bo'lardi).
 */
export type AttendancePass = AsSent<AttendancePassDto>

/**
 * Ishdan ketgan xodimning o'tishi — XAVFSIZLIK hodisasi (§6.5, §8.7).
 * Bitta ODAM — bitta yozuv, eng so'nggi o'tishi bilan; `passCount` — nechta.
 */
export type FormerEmployeeAlert = AsSent<FormerEmployeeAlertDto>

/** `count` — ODAMLAR soni (`items.length`), o'tishlar emas. */
export type FormerEmployeeAlerts = AsSent<FormerEmployeeAlertsDto>

/* ════════════════════════════════════════ davomat statistikasi (dashboard) */

/**
 * Xodimning shu kundagi holati — oltita (diagrammaning oltita bo'lagi).
 * Legenda tartibi `DASHBOARD_STATUSES` da (`attendance-presentation.ts`).
 */
export type DashboardStatus = AttendanceDashboardStatus

/**
 * Saralash: `name` — A→Z (server sukuti), `-name` — Z→A.
 *
 * KLIENT TOROYTIRISHI: server `sort` ni oddiy satr sifatida bog'laydi va
 * OpenAPI da u sanoq emas (`sort?: string`). Ikki qiymatdan boshqasini
 * yuborishning ma'nosi yo'q — tip buni kompilyatsiyada ushlaydi.
 */
export type DashboardSort = 'name' | '-name'

/**
 * Holatlar bo'yicha sanoq. ⚠️ `search` va `status` filtrlariga BO'YSUNMAYDI
 * (server shartnomasi): diagramma bo'lagi bosilganda qolgan bo'laklar
 * yo'qolib qolmasligi kerak. Oltitasining yig'indisi `total` ga teng.
 */
export type DashboardCounts = AsSent<AttendanceDashboardCountsDto>

/**
 * Jadvaldagi bitta xodim. `lateMinutes` faqat `Late` da to'ladi;
 * `arrivedAt`/`leftAt` esa HAR QANDAY holatda bo'lishi mumkin (dam olish
 * kuni yoki jadvalsiz kunda ham kelganlar bor) — ekran vaqtni holatga
 * qarab yashirmaydi.
 */
export type DashboardItem = AsSent<AttendanceDashboardItemDto>

/**
 * Statistika javobi. `totalItems`/`pageCount` — filtrlangan jadval
 * (qidiruv va holat bilan), `total`/`counts` — diagramma (ularsiz).
 * `asOf` — hisob lahzasi: «Yangilandi: HH:mm» shundan, brauzer soatidan emas.
 */
export type AttendanceDashboard = AsSent<AttendanceDashboardDto>

/** So'rov parametrlari — generatsiya qilinganidan, `sort` toraytirilgan holda. */
export type DashboardFilters = Omit<AsSent<GetAttendanceDashboardParams>, 'sort'> & {
  sort?: DashboardSort
}

/**
 * Davr grafigining bitta kuni. `counts` paneldagi bilan AYNAN bir xil
 * shakl — ikkala ekran bitta rang xaritasidan (`DASHBOARD_STATUS_LOOK`)
 * o'qiydi va bir holat ikki joyda ikki xil rangda chiqa olmaydi.
 */
export type TrendDay = AsSent<AttendanceTrendDayDto>

/**
 * Davr grafigi. ⚠️ `days` da oraliqning HAR BIR sanasi bor, tirqishsiz
 * (server shartnomasi) — ya'ni x o'qini klient to'ldirmaydi va ta'til
 * haftasi grafikdan yo'qolib qolmaydi.
 */
export type AttendanceTrend = AsSent<AttendanceTrendDto>

/** So'rov parametrlari — `from` va `to` MAJBURIY (server shartnomasi). */
export type TrendFilters = AsSent<GetAttendanceTrendParams>

/**
 * Yaqinlashib kelayotgan bitta tug'ilgan kun.
 *
 * ⚠️ Bu bosh sahifadagi `DashboardBirthday` EMAS: u faqat BUGUNGI kunni
 * biladi va org-filtri yo'q. Ikkalasi bir xil ko'rinsa ham bir xil savolga
 * javob bermaydi — shu sababli ikkita tip, ikkita endpoint.
 *
 * `daysUntil` — bugun uchun 0. Sana klientda HISOBLANMAYDI: «bugun»
 * serverda, Toshkent vaqti bo'yicha aniqlanadi va brauzer sanasi undan
 * farq qilishi mumkin.
 */
export type UpcomingBirthday = AsSent<AttendanceBirthdayDto>

/**
 * So'rov parametrlari — DAVRSIZ, ataylab.
 *
 * Panel «tanlangan oraliqda kimning tug'ilgan kuni bo'lgan» degan savolga
 * emas, «keyingi navbatda kim» degan savolga javob beradi: davr
 * almashtirilganda ro'yxat qayta so'ralmaydi (kesh kaliti
 * `attendance-queries.ts` da).
 */
export type BirthdayFilters = AsSent<GetUpcomingBirthdaysParams>

/* ═════════════════════════════════════════════════════════ hisobotlar (§6.6) */

/**
 * Tabel katagi — oyning HAR BIR kuni uchun keladi, yozuvi yo'q kunlar
 * serverda hosil qilinadi (§5.7.1). «0 olingan, 0 ayirilgan» tanaffus IKKI
 * ma'noli (oyna yo'q yoki oyna paytida binoda bo'lmagan) — ekran bugungi
 * jadvalni o'qib uni «to'g'rilamaydi».
 */
export type TimesheetCell = AsSent<TimesheetCellDto>

export type TimesheetTotals = AsSent<TimesheetTotalsDto>

export type TimesheetRow = AsSent<TimesheetRowDto>

/** `total`/`pageCount` — XODIMLAR bo'yicha; `lastComputedAt` — sahifadagi eng so'nggi hisob. */
export type Timesheet = AsSent<AttendanceTimesheetDto>

export type AttendanceExceptionRow = AsSent<AttendanceExceptionDto>

/** `overtimeMinutes` hisoblanadi, lekin HECH QACHON pulga ko'paytirilmaydi (§1.2 #2). */
export type AttendanceSummaryRow = AsSent<AttendanceSummaryDto>

/* ═══════════════════════════════════════════ eksport va qayta hisoblash (§6.7) */

/**
 * Eksport natijasi: baytlar va ular qanday nom bilan saqlanishi.
 *
 * KLIENT TIPI: server faylni `Content-Disposition` bilan yuboradi, DTO
 * emas — OpenAPI da bu javobning modeli yo'q.
 */
export interface AttendanceExportFile {
  blob: Blob
  fileName: string
}

/** `employeeId: null` — oraliqdagi BARCHA xodimlar. */
export type RecomputeRequest = Required<RecomputeAttendanceRequest>

/** `202 Accepted` — «boshlandi», «tayyor» emas (§6.7). */
export type RecomputeResult = AsSent<AttendanceRecomputeAcceptedDto>

/* ════════════════════════════════════════════════════════════════ filtrlar */

/*
 * Filtrlar — generatsiya qilingan so'rov parametrlaridan. EKSPORT
 * parametrlari asos qilib olingan: ular ro'yxat parametrlarining o'zi,
 * faqat sahifalashsiz — «ekranda nima ko'rsang, faylda ham shu» qoidasi
 * tip darajasida ham bitta manbadan.
 */

/** Sahifalash — `page` 1 dan, `pageSize` eng ko'pi 200 (SPEC §6). */
export type PageParams = AsSent<Required<Pick<SearchAttendancePassesParams, 'page' | 'pageSize'>>>

/**
 * `matched` UCH holatli: `true` — moslangan, `false` — moslanmagan, yo'q —
 * hammasi. `false` so'rovga TUSHADI (u haqiqiy filtr qiymati).
 */
export type PassFilters = ExportAttendancePassesParams

export type PassListFilters = PassFilters & PageParams

/** `year` va `month` MAJBURIY — tabel doim bitta oy. */
export type TimesheetFilters = AsSent<ExportAttendanceTimesheetParams>

export type TimesheetListFilters = TimesheetFilters & PageParams

/**
 * `kind` generatsiya qilingan tipda ixtiyoriy satr (`ExceptionKind` izohi);
 * klient uni MAJBURIY va sanoq qilib toraytiradi — `kind` siz so'rov
 * serverda baribir `davomat.hisobot_turi_notogri` bo'lardi.
 */
export type ExceptionFilters = Omit<AsSent<ExportAttendanceExceptionsParams>, 'kind'> & {
  kind: ExceptionKind
}

export type ExceptionListFilters = ExceptionFilters & PageParams

/**
 * Davr klientda MAJBURIY. Server davrsiz so'rovni «oy boshidan bugungacha»
 * deb oladi, lekin ekran uni doim ochiq yuboradi: jadval, eksport fayli va
 * uning nomi (`rangeTag`) aynan bir xil sanalarni ko'rsatsin — sukutga
 * tayanilsa, fayl nomi bo'sh davr bilan chiqardi.
 */
export type SummaryFilters = ExportAttendanceSummaryParams & { from: string; to: string }

export type SummaryListFilters = SummaryFilters & PageParams

export type IdentityFilters = Omit<ListAttendanceIdentitiesParams, 'page' | 'pageSize'>

export type IdentityListFilters = IdentityFilters & PageParams

export type AssignmentFilters = ListScheduleAssignmentsParams

/* ══════════════════════════════════════════════════════════════ yordamchilar */

/**
 * Bo'sh satrli filtrni so'rovdan chiqaradi.
 *
 * `undefined` ni axios o'zi tashlaydi, `''` ni esa YO'Q — `from=` bilan
 * ketgan so'rov serverda `DateOnly` sifatida o'qilmay 400 bo'lardi.
 * ⚠️ `false` TASHLANMAYDI: `matched: false` — «moslanmaganlar».
 */
function withoutBlanks<T extends object>(source: T): T {
  return Object.fromEntries(
    Object.entries(source).filter(([, value]) => value !== undefined && value !== ''),
  ) as T
}

/**
 * `TimeOnly` → «HH:mm».
 *
 * Server «09:00:00» yuboradi, forma esa `<input type="time">` da aynan
 * `HH:mm` ni kutadi va soniyali satrni jimgina bo'sh maydon qilib
 * ko'rsatardi. Qirqish o'qishda BIR MARTA — har chizadigan joy o'zicha
 * kesib yursa, biri albatta unutilardi.
 */
function clock(value: string | null): string | null {
  if (value === null) return null

  const match = /^(\d{1,2}):(\d{2})/u.exec(value)
  return match === null ? value : `${match[1]?.padStart(2, '0') ?? '00'}:${match[2] ?? '00'}`
}

function toScheduleDay(day: WorkScheduleDay): WorkScheduleDay {
  return {
    ...day,
    startTime: clock(day.startTime),
    endTime: clock(day.endTime),
    breakStart: clock(day.breakStart),
    breakEnd: clock(day.breakEnd),
  }
}

function toSchedule(dto: WorkScheduleDto): WorkSchedule {
  const schedule = asSent(dto)

  return {
    ...schedule,
    // Muharrir kunlarni dushanbadan yakshanbagacha chizadi — tartib shu yerda qotiriladi
    days: schedule.days.map(toScheduleDay).sort((left, right) => left.dayIndex - right.dayIndex),
  }
}

/* ══════════════════════════════════════════════════════════════════ yo'llar */

/**
 * Faqat generatsiya qilingan chaqiruvi ISHLATILMAYDIGAN yo'llar uchun:
 * blob javoblar (surat, eksport — sarlavha kerak) va multipart import
 * (`Content-Type` ni axios o'zi yozishi shart — `http-client.ts` izohi).
 */
const BASE = '/api/attendance'

/* ═════════════════════════════════════════════════════ qurilmalar (§6.2) */

export async function fetchDevices(
  includeInactive: boolean,
  signal?: AbortSignal,
): Promise<AttendanceDevice[]> {
  return asSent(await listAttendanceDevices({ includeInactive }, signal))
}

export async function fetchDevice(id: string, signal?: AbortSignal): Promise<AttendanceDevice> {
  return asSent(await getAttendanceDevice(id, signal))
}

/** Javobda TOKEN bor — u boshqa hech qachon qaytarilmaydi (SPEC §3.2). */
export async function createDevice(
  body: AttendanceDeviceRequest,
): Promise<AttendanceDeviceCreated> {
  return asSent(await registerAttendanceDevice(body))
}

export async function updateDevice(
  id: string,
  body: AttendanceDeviceRequest,
): Promise<AttendanceDevice> {
  return asSent(await updateAttendanceDevice(id, body))
}

/**
 * Tokenni yangilash — eski token SHU ZAHOTI o'ladi, ekran buni ochiq
 * aytishi kerak (aks holda turniket jimgina uziladi).
 */
export async function rotateDeviceToken(id: string): Promise<AttendanceDeviceCreated> {
  return asSent(await rotateAttendanceDeviceToken(id))
}

export async function setDeviceActive(id: string, active: boolean): Promise<AttendanceDevice> {
  return asSent(active ? await activateAttendanceDevice(id) : await deactivateAttendanceDevice(id))
}

/**
 * Yumshoq o'chirish. O'tishi BOR qurilma o'chirilmaydi
 * (`davomat.qurilma_otishlari_bor`, 409) — u FAOLSIZLANTIRILADI.
 */
export async function deleteDevice(id: string): Promise<void> {
  await deleteAttendanceDevice(id)
}

/**
 * Ofisdagi agentning `.env` qatorlari — token tayyor holda (SPEC-agent §E).
 *
 * NEGA klientda quriladi: server o'z tashqi manzilini bilmaydi (u Caddy
 * ortida), `baseURL` ni esa frontend biladi. Prod'da `baseURL` bo'sh satr
 * (bir xil domen) — shuning uchun `??` emas, `||`.
 *
 * Terminalning IP manzili va paroli BO'SH qoladi: ular ofis tarmog'iniki
 * va serverga hech qachon kelmaydi.
 */
export function buildAgentEnv(token: string): string {
  const base = (api.defaults.baseURL || window.location.origin).replace(/\/+$/u, '')
  return [
    `AGENT__APIBASEURL=${base}`,
    `AGENT__DEVICETOKEN=${token}`,
    'AGENT__DEVICEHOST=',
    'AGENT__DEVICEUSER=admin',
    'AGENT__DEVICEPASSWORD=',
  ].join('\n')
}

/* ═══════════════════════════════════════════════════════ mosliklar (§6.3) */

export async function fetchIdentities(filters: IdentityListFilters, signal?: AbortSignal) {
  return asSent(await listAttendanceIdentities(withoutBlanks(filters), signal))
}

/**
 * Moslik yaratish — server shu zahoti eski moslanmagan o'tishlarni
 * bog'laydi va kunlarni qayta hisoblaydi (§4.3). Alohida «qayta hisobla»
 * tugmasi kerak emas.
 */
export async function createIdentity(body: CreateIdentityRequest): Promise<AttendanceIdentity> {
  return asSent(await createAttendanceIdentity(body))
}

/** Uzish (yumshoq). O'tgan o'tishlar YECHILGANICHA qoladi (§4.3). */
export async function deleteIdentity(id: string): Promise<void> {
  await deleteAttendanceIdentity(id)
}

export async function fetchUnmatched(page: PageParams, signal?: AbortSignal) {
  return asSent(await listUnmatchedAttendancePersons(page, signal))
}

/**
 * Verifix ro'yxatini oldindan ko'rish — HECH NARSA YOZILMAYDI (§4.4).
 *
 * Generatsiya qilingan chaqiruv ATAYLAB ishlatilmagan: u
 * `Content-Type: multipart/form-data` ni qo'lda qo'yadi, chegara (boundary)
 * esa faqat sarlavhasiz so'rovda brauzer tomonidan yoziladi
 * (`http-client.ts` — xodim rasmi aynan shu sababdan yuklanmagan edi).
 * Javob tipi baribir generatsiya qilingan modeldan.
 */
export async function previewIdentityImport(
  file: File,
  provider: AttendanceIdentityProvider,
  signal?: AbortSignal,
): Promise<ImportPreview> {
  const form = new FormData()
  form.append('file', file)
  /*
    `provider` serverda ixtiyoriy (sukut `Verifix`), lekin DOIM yuboriladi:
    tasdiqlash AYNAN shu qiymat bilan qaytarilishi shart va sukutga
    tayanish bu bog'liqlikni ko'rinmas qilardi.
  */
  form.append('provider', provider)

  const { data } = await api.post<IdentityImportPreviewDto>(`${BASE}/identities/import`, form, {
    signal,
  })
  return asSent(data)
}

/** Operator tasdiqlagan qatorlar. Har bir yaratilgan moslik backfill'ni qo'zg'atadi. */
export async function confirmIdentityImport(body: ImportConfirmRequest): Promise<ImportResult> {
  return asSent(await confirmAttendanceIdentityImport(body))
}

/* ═══════════════════════════════════════════════════════ jadvallar (§6.4) */

export async function fetchSchedules(signal?: AbortSignal): Promise<WorkSchedule[]> {
  return (await listWorkSchedules(signal)).map(toSchedule)
}

export async function fetchSchedule(id: string, signal?: AbortSignal): Promise<WorkSchedule> {
  return toSchedule(await getWorkSchedule(id, signal))
}

/** Yaratish va tahrirlash bitta funksiya: tanasi ham, javobi ham bir xil. */
export async function saveSchedule(
  id: string | null,
  body: WorkScheduleRequest,
): Promise<WorkSchedule> {
  return toSchedule(id === null ? await createWorkSchedule(body) : await updateWorkSchedule(id, body))
}

/**
 * Jadvalni yoqish/o'chirish (§6.4 da yo'q, B6 qo'shgan).
 *
 * ⚠️ Faolsizlantirish DELETE bilan bir xil qoida bilan rad etiladi:
 * biriktirmasi bor (muddati o'tganlari ham) jadval —
 * `davomat.jadval_biriktirilgan` (409). Sabab: hisob mexanizmi `isActive`
 * ga qaramaydi, ya'ni ruxsat berilsa ekran «nofaol» deb, tabel esa shu
 * jadval bo'yicha hisoblab turardi.
 */
export async function setScheduleActive(id: string, active: boolean): Promise<WorkSchedule> {
  return toSchedule(active ? await activateWorkSchedule(id) : await deactivateWorkSchedule(id))
}

/** Biriktirmasi bor jadval o'chirilmaydi (`davomat.jadval_biriktirilgan`, 409). */
export async function deleteSchedule(id: string): Promise<void> {
  await deleteWorkSchedule(id)
}

export async function fetchAssignments(
  filters: AssignmentFilters,
  signal?: AbortSignal,
): Promise<ScheduleAssignment[]> {
  return asSent(await listScheduleAssignments(withoutBlanks(filters), signal))
}

/** Faolsiz jadvalni biriktirib bo'lmaydi (`davomat.jadval_faolsiz`, 409). */
export async function createAssignment(
  body: ScheduleAssignmentRequest,
): Promise<ScheduleAssignment> {
  return asSent(await createScheduleAssignment(body))
}

export async function deleteAssignment(id: string): Promise<void> {
  await deleteScheduleAssignment(id)
}

/** «Kim uchun amal qiladi» — yutgan jadval VA yutgan daraja (§6.4). */
export async function resolveSchedule(
  employeeId: string,
  date: string,
  signal?: AbortSignal,
): Promise<ScheduleResolution> {
  const resolution = asSent(await resolveWorkSchedule({ employeeId, date }, signal))
  return { ...resolution, day: resolution.day === null ? null : toScheduleDay(resolution.day) }
}

/* ═══════════════════════════════════════════════════════ o'tishlar (§6.5) */

export async function fetchPasses(filters: PassListFilters, signal?: AbortSignal) {
  return asSent(await searchAttendancePasses(withoutBlanks(filters), signal))
}

/**
 * Bitta o'tish — `?passId=` havolasi (§6.5).
 *
 * ⚠️ 404 bu yerda `null` ga AYLANTIRILMAYDI (`fetchPassPhoto` dan farqi):
 * `davomat.otish_topilmadi` — saqlash muddati tugab o'chirilgan o'tish,
 * oyna aynan shu tarjima qilingan sababni ko'rsatadi.
 */
export async function fetchPass(id: string, signal?: AbortSignal): Promise<AttendancePass> {
  return asSent(await getAttendancePass(id, signal))
}

/**
 * Faqat ishdan ketganlar ogohlantirishi (§6.5 / §8.7).
 *
 * NEGA taxta emas: menyu nishoni HAR ekranda 30 soniyada so'raydi, taxta
 * esa har safar ichkaridagilar, kutilganlar va kechikkanlarni qayta
 * hisoblatardi.
 */
export async function fetchFormerEmployeeAlerts(
  signal?: AbortSignal,
): Promise<FormerEmployeeAlerts> {
  const alerts = asSent(await getFormerEmployeeAlerts(signal))

  /*
    Eng yangisi tepada — server shunday beradi, lekin bu xavfsizlik
    bloki: tepada kechagi voqea turib, hozirgisi pastga tushib qolsa, u
    o'z vazifasini bajarmaydi. Saralash arzon, kafolat esa klientda.
  */
  return {
    ...alerts,
    items: [...alerts.items].sort(
      (left, right) => instant(right.occurredAt) - instant(left.occurredAt),
    ),
  }
}

/**
 * Lahza → son. Satr sifatida solishtirilmaydi: «…12Z» va «…12.345+00:00»
 * bir lahza, lekin alifbo bo'yicha boshqacha tartiblanadi.
 */
function instant(value: string): number {
  const parsed = Date.parse(value)
  return Number.isNaN(parsed) ? 0 : parsed
}

/**
 * O'tish surati — BAYTLAR, havola emas.
 *
 * `<img src="/api/…">` ishlamaydi: brauzer `<img>` so'roviga
 * `Authorization` qo'sha olmaydi. Generatsiya qilingan chaqiruv `void`
 * qaytaradi va `responseType: 'blob'` bermaydi — shu sababli axios.
 *
 * `404` — XATO EMAS: surat bo'lmagan yoki tozalangan (§3.10). Ikkalasi
 * `null`; qaysi biri ekanini chaqiruvchi `photoPurgedAt` dan biladi.
 */
export async function fetchPassPhoto(id: string, signal?: AbortSignal): Promise<Blob | null> {
  try {
    const { data } = await api.get<Blob>(`${BASE}/passes/${id}/photo`, {
      responseType: 'blob',
      signal,
    })
    return data
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) return null
    throw error
  }
}

/* ════════════════════════════════════════ davomat statistikasi (dashboard) */

/**
 * Davomat statistikasi — diagramma sanoqlari va jadval sahifasi BITTA
 * javobda: ikkalasi bir lahzaning (`asOf`) surati, ya'ni «bo'lakda 75,
 * ro'yxatda 74» degan holat bo'lmaydi.
 *
 * Noto'g'ri `status` yoki `date` — 400 (server); manzil tekshiruvi
 * (`validateDashboardSearch`) ularni so'rovga yetkazmaydi.
 */
export async function fetchDashboard(
  filters: DashboardFilters,
  signal?: AbortSignal,
): Promise<AttendanceDashboard> {
  return asSent(await getAttendanceDashboard(withoutBlanks(filters), signal))
}

/**
 * Davr grafigi — bir necha kunlik davr tanlanganda panelning O'RNIGA
 * so'raladi (ikkalasi birga emas: ekranda yo jadval, yo kunlik chiziq).
 *
 * Teskari yoki 92 kundan uzun davr — 400 (`davomat.davr_notogri`,
 * `davomat.davr_uzun`). Presetlar bunday oraliq yasay olmaydi; qo'lda
 * kiritilgani esa tugma bosilishidan OLDIN ushlanadi (`isRangeTooLong`,
 * `isRangeInverted`) — server tekshiruvi baribir joyida qoladi.
 */
export async function fetchTrend(
  filters: TrendFilters,
  signal?: AbortSignal,
): Promise<AttendanceTrend> {
  return asSent(await getAttendanceTrend(withoutBlanks(filters), signal))
}

/**
 * Yaqinlashib kelayotgan tug'ilgan kunlar — eng yaqinidan boshlab.
 *
 * Tartib SERVERDA qo'yilgan va klientda qayta saralanmaydi: «eng yaqini»
 * yil chegarasi va 29-fevral qoidasini biladi, ularni ikkinchi marta
 * yozish esa ikki javobni ajratib yuborardi.
 *
 * `limit` chegaradan chiqsa server RAD ETMAYDI, qisadi (sukut 10, eng
 * ko'pi 50) — panel xato ekraniga aylanmasligi kerak.
 */
export async function fetchUpcomingBirthdays(
  filters: BirthdayFilters,
  signal?: AbortSignal,
): Promise<UpcomingBirthday[]> {
  return asSent(await getUpcomingBirthdays(withoutBlanks(filters), signal))
}

/* ═══════════════════════════════════════════════ yillik to'r (§C) */

/**
 * Yillik to'rning bitta katagi.
 *
 * `plannedMinutes` — kun normasi, SERVERDA hisoblangan (tanaffus
 * ayirilgan, tungi smena uzunligi bilan). Klient uni soatga aylantiradi,
 * lekin QAYTA hisoblamaydi.
 *
 * `source` — katak naqshdanmi (`Pattern`) yoki shu sanaga qo'lda
 * yozilganmi (`Date`). Ekran shu bo'yicha kulrang va to'q kataklarni
 * ajratadi.
 */
export type ScheduleYearDay = AsSent<ScheduleYearDayDto>

/** Jadvalning bir yillik to'ri — 365/366 katak, tirqishsiz. */
export type ScheduleYear = AsSent<ScheduleYearDto>

/** Kataklarni yozish yoki tozalash tanasi. */
export type ScheduleDatesInput = AsSent<ScheduleDatesBody>

export async function fetchScheduleYear(
  scheduleId: string,
  year: number,
  signal?: AbortSignal,
): Promise<ScheduleYear> {
  return asSent(await getWorkScheduleYear(scheduleId, { year }, signal))
}

/**
 * Kataklarni yozish yoki tozalash.
 *
 * ⚠️ Serverda yozuvdan keyin BUGUNGACHA bo'lgan kunlar qayta hisoblanadi
 * — tabel, kalendar va panel javoblari eskiradi. Kesh `providers.tsx`
 * orqali o'zi bekor qilinadi.
 */
export async function putScheduleDates(scheduleId: string, body: ScheduleDatesInput) {
  await setWorkScheduleDates(scheduleId, body)
}

/**
 * Yilni haftalik naqshdan to'ldirish (SPEC-jadval-to'ri §D).
 *
 * Faqat katagi YOZILMAGAN sanalar yoziladi — qo'lda yozilganlari joyida
 * qoladi. Hisob o'zgarmaydi (katak naqsh bilan bir xil), shu sababli
 * server qayta hisoblashni navbatga qo'ymaydi. «Yilni tozalash» uchun
 * alohida chaqiruv yo'q: u `putScheduleDates` ning `reset: true` i.
 */
export async function postScheduleYearFill(scheduleId: string, year: number) {
  await fillWorkScheduleYear(scheduleId, { year })
}

/* ═══════════════════════════════════════════ yo'qlik so'rovlari (§B) */

/**
 * Bitta yo'qlik so'rovi.
 *
 * `fromTime`/`toTime` — faqat BIR KUNLIK so'rovda bo'lishi mumkin (server
 * shartnomasi): ko'p kunlik so'rovda vaqt 400 bilan rad etiladi.
 *
 * `version` — parallellik hisoblagichi (SPEC-sorov-tahriri §3.3): HR tahrir
 * va qarorda AYNAN ko'rgan raqamini qaytaradi. Xodim shu orada ilovada
 * o'zgartirgan bo'lsa server 409 `davomat.sorov_ozgargan` beradi — HR
 * eskirgan nusxani tasdiqlab qo'ymaydi. `editedAt`/`editedByEmployee` —
 * «Ilovada o'zgartirilgan» belgisi (navbat qatori va qaror oynasi).
 */
export type AbsenceRequest = AsSent<AbsenceRequestDto>

/** Ro'yxat so'rovining parametrlari. */
export type AbsenceRequestFilters = AsSent<SearchAbsenceRequestsParams>

/** Yaratish va tahrirlash — server uchun BITTA shakl. */
export type AbsenceRequestInput = AsSent<AbsenceRequestBody>

/** Qaror: tasdiqlash yoki rad etish. */
export type AbsenceDecisionInput = AsSent<AbsenceDecisionBody>

/** Kutayotgan so'rovlar soni — nav nishoni uchun. */
export type AbsenceRequestCount = AsSent<AbsenceRequestCountDto>

export const ABSENCE_REQUEST_STATUSES: readonly AbsenceRequestStatus[] =
  Object.values(AbsenceRequestStatus)

/**
 * So'rov shakli — Verifix «Часть дня / Весь день / Несколько дней»
 * (SPEC-jadval-sorovi §B). Server shartnomasi O'ZGARMAGAN: shakl sanalar va
 * vaqtlardan hosil qilinadi, ro'yxat esa `mode` bo'yicha filtrlanadi.
 */
export const ABSENCE_REQUEST_MODES: readonly AbsenceRequestMode[] = Object.values(AbsenceRequestMode)

export async function fetchAbsenceRequests(
  filters: AbsenceRequestFilters,
  signal?: AbortSignal,
) {
  return asSent(await searchAbsenceRequests(withoutBlanks(filters), signal))
}

/**
 * Kutayotgan so'rovlar soni.
 *
 * Ro'yxat emas, faqat son: nishon har ekranda turadi va ro'yxatni yuklash
 * unga kerak emas (`fetchFormerEmployeeAlerts` bilan bir xil naqsh).
 */
export async function fetchPendingRequestCount(signal?: AbortSignal) {
  return asSent(await getPendingAbsenceRequestCount(signal))
}

export async function postAbsenceRequest(body: AbsenceRequestInput) {
  return asSent(await createAbsenceRequest(body))
}

export async function putAbsenceRequest(id: string, body: AbsenceRequestInput) {
  return asSent(await updateAbsenceRequest(id, body))
}

/**
 * Qaror — tasdiqlash yoki rad etish.
 *
 * ⚠️ Qarordan keyin SERVER so'rov qamragan kunlarni qayta hisoblaydi:
 * tabel, kalendar va panel javoblari eskiradi, ya'ni chaqiruvchi davomat
 * keshini bekor qilishi shart (`attendance-queries.ts`).
 */
export async function postAbsenceDecision(id: string, body: AbsenceDecisionInput) {
  return asSent(await decideAbsenceRequest(id, body))
}

export async function postAbsenceCancel(id: string) {
  await cancelAbsenceRequest(id)
}

/* ═════════════════════════════════════ jadval so'rovlari (SPEC-jadval-sorovi §C) */

/**
 * Jadval so'rovining bir kuni — vaqtlar «HH:mm» (o'qishda qirqiladi, `clock`
 * izohi). `swapWith` — faqat almashtirishda: juftining sanasi.
 */
export type ScheduleChangeDay = AsSent<ScheduleChangeDayDto>

/**
 * Jadvalni o'zgartirish so'rovi. Holat, qaror va bekor qilish qoidalari yo'qlik
 * so'rovi bilan AYNAN bir xil (holat turi ham o'sha — `AbsenceRequestStatus`),
 * `version` va tahrir belgisi ham (`AbsenceRequest` izohi).
 */
export type ScheduleChangeRequest = AsSent<ScheduleChangeRequestDto>

/** Ro'yxat parametrlari — yo'qlik so'rovlaridagi filtrlar, `absenceTypeId`/`mode` siz. */
export type ScheduleChangeRequestFilters = AsSent<SearchScheduleChangeRequestsParams>

/**
 * Yaratish va tahrirlash tanasi. Vaqtlar «HH:mm:ss», sanalar «YYYY-MM-DD».
 * `kind: Change` da `days`, `kind: Swap` da `swaps` to'ldiriladi.
 */
export type ScheduleChangeRequestInput = ScheduleChangeRequestBody

export const SCHEDULE_CHANGE_KINDS: readonly ScheduleChangeKind[] = Object.values(ScheduleChangeKind)

function toScheduleChangeRequest(dto: ScheduleChangeRequestDto): ScheduleChangeRequest {
  const request = asSent(dto)

  return {
    ...request,
    days: request.days.map((day) => ({
      ...day,
      startTime: clock(day.startTime),
      endTime: clock(day.endTime),
      breakStart: clock(day.breakStart),
      breakEnd: clock(day.breakEnd),
    })),
  }
}

export async function fetchScheduleChangeRequests(
  filters: ScheduleChangeRequestFilters,
  signal?: AbortSignal,
) {
  const page = asSent(await searchScheduleChangeRequests(withoutBlanks(filters), signal))
  return { ...page, items: page.items.map(toScheduleChangeRequest) }
}

/**
 * Almashtirishda server ikkala sananing AMALDAGI rejasini suratga oladi —
 * sanada jadval bo'lmasa `davomat.jadval_sorovi_jadval_yoq` (400).
 */
export async function postScheduleChangeRequest(body: ScheduleChangeRequestInput) {
  return toScheduleChangeRequest(await createScheduleChangeRequest(body))
}

/**
 * Faqat kutayotgan so'rov (`davomat.sorov_qaror_qabul_qilingan`, 409). Tanada
 * HR ko'rgan `version` — xodim shu orada ilovada o'zgartirgan bo'lsa
 * `davomat.sorov_ozgargan` (409, SPEC-sorov-tahriri §3.3).
 */
export async function putScheduleChangeRequest(id: string, body: ScheduleChangeRequestInput) {
  return toScheduleChangeRequest(await updateScheduleChangeRequest(id, body))
}

/**
 * Qaror. ⚠️ Tasdiqlangan kun xodimning o'sha sanadagi rejasi O'RNIGA turadi va
 * kunlar serverda qayta hisoblanadi — tabel, kalendar va panel eskiradi
 * (`postAbsenceDecision` bilan bir xil; kesh `providers.tsx` da bekor qilinadi).
 */
export async function postScheduleChangeDecision(id: string, body: AbsenceDecisionInput) {
  return toScheduleChangeRequest(await decideScheduleChangeRequest(id, body))
}

/** Tasdiqlangani ham bekor qilinadi — kunlar jadvalning o'z rejasiga qaytadi. */
export async function postScheduleChangeCancel(id: string) {
  await cancelScheduleChangeRequest(id)
}

/**
 * Jadval so'rovidagi tanaffus qoidasi (SPEC-sorov-tahriri §4) — «Kunni
 * o'zgartirish» ning ish kunida tanaffus `from`–`to` ichida, aniq `minutes`
 * daqiqa, jumada `minutes`…`fridayMaxMinutes`; tanaffussiz kun ham mumkin.
 *
 * Vaqtlar «HH:mm» — o'qishda qirqiladi (`clock` izohi): xabar matnida
 * «12:00:00» emas, «12:00» chiqishi kerak.
 *
 * ⚠️ Forma bu bilan faqat OLDINDAN ogohlantiradi; yakuniy so'z serverda
 * (`davomat.jadval_sorovi_tanaffus_oraligi`, `…_davomiyligi`).
 */
export type RequestBreakRule = AsSent<RequestBreakRuleDto>

export async function fetchScheduleRequestBreakRule(
  signal?: AbortSignal,
): Promise<RequestBreakRule> {
  const rule = asSent(await getScheduleRequestBreakRule(signal))
  return { ...rule, from: clock(rule.from) ?? rule.from, to: clock(rule.to) ?? rule.to }
}

/* ═════════════════════════════════════════════════════ hisobotlar (§6.6) */

export async function fetchTimesheet(
  filters: TimesheetListFilters,
  signal?: AbortSignal,
): Promise<Timesheet> {
  return asSent(await getAttendanceTimesheet(withoutBlanks(filters), signal))
}

export async function fetchExceptions(filters: ExceptionListFilters, signal?: AbortSignal) {
  return asSent(await getAttendanceExceptions(withoutBlanks(filters), signal))
}

export async function fetchSummary(filters: SummaryListFilters, signal?: AbortSignal) {
  return asSent(await getAttendanceSummary(withoutBlanks(filters), signal))
}

/* ═══════════════════════════════════════ eksport va qayta hisoblash (§6.7) */

/**
 * Excel eksporti — to'rt hisobot uchun bitta quvur.
 *
 * Parametrlar EKRANDAGI so'rov bilan BITTA tipdan (`PassFilters` …),
 * sahifalash yuborilmaydi. Generatsiya qilingan eksport chaqiruvi `void`
 * qaytaradi va sarlavhani bermaydi — fayl nomi esa `Content-Disposition`
 * da, shu sababli axios. Nomga TAYANIB qolinmaydi: sarlavha kelmasa zaxira
 * nom SPEC §6.7 shaklida.
 */
async function download(
  path: string,
  query: object,
  fallbackName: string,
  signal?: AbortSignal,
): Promise<AttendanceExportFile> {
  try {
    const response = await api.get<Blob>(`${BASE}/export/${path}`, {
      params: withoutBlanks(query),
      responseType: 'blob',
      signal,
    })

    const disposition = response.headers['content-disposition']

    return {
      blob: response.data,
      fileName:
        fileNameFromDisposition(typeof disposition === 'string' ? disposition : null) ??
        fallbackName,
    }
  } catch (error) {
    await revealProblemDetails(error)
    throw error
  }
}

/**
 * Blob so'rovidagi XATO tanasini o'qiladigan holga keltiradi.
 *
 * `responseType: 'blob'` xatoga ham tegishli: `{"title":"davomat.davr_uzun"}`
 * ham `Blob` bo'lib o'raladi va `problemCode` undan `title` ni topolmaydi —
 * HR «oraliqni qisqartiring» o'rniga «Nimadir noto'g'ri ketdi» ni ko'rardi.
 *
 * ⚠️ `employees-api.ts` dagi yordamchining IKKINCHI nusxasi, bilib
 * qilingan: u yerda `private`. Uchinchi nusxa paydo bo'lganda ko'chirish
 * o'rni — `shared/api/problem-details.ts`.
 */
async function revealProblemDetails(error: unknown): Promise<void> {
  if (!axios.isAxiosError(error)) return

  const response = error.response
  if (response === undefined || !(response.data instanceof Blob)) return

  try {
    response.data = JSON.parse(await response.data.text())
  } catch {
    // Tana JSON emas (proksining HTML sahifasi yoki bo'sh javob) — xato o'z
    // holicha yuqoriga ketadi va umumiy xabarga aylanadi
  }
}

/** «2026-09» — server nomining shaklini takrorlaydi (§6.7). */
function monthTag(year: number, month: number): string {
  return `${year}-${String(month).padStart(2, '0')}`
}

/** «2026-09-01_2026-09-17» — oraliqli fayllar uchun. */
function rangeTag(from: string | undefined, to: string | undefined): string {
  return [from, to].filter((value) => value !== undefined && value !== '').join('_')
}

export function exportTimesheet(
  filters: TimesheetFilters,
  signal?: AbortSignal,
): Promise<AttendanceExportFile> {
  return download(
    'timesheet',
    filters,
    `Bonvi_davomat_tabel_${monthTag(filters.year, filters.month)}.xlsx`,
    signal,
  )
}

export function exportPasses(
  filters: PassFilters,
  signal?: AbortSignal,
): Promise<AttendanceExportFile> {
  return download(
    'passes',
    filters,
    `Bonvi_davomat_otishlar_${rangeTag(filters.from, filters.to)}.xlsx`,
    signal,
  )
}

export function exportExceptions(
  filters: ExceptionFilters,
  signal?: AbortSignal,
): Promise<AttendanceExportFile> {
  return download(
    'exceptions',
    filters,
    `Bonvi_davomat_chetlanishlar_${rangeTag(filters.from, filters.to)}.xlsx`,
    signal,
  )
}

export function exportSummary(
  filters: SummaryFilters,
  signal?: AbortSignal,
): Promise<AttendanceExportFile> {
  return download(
    'summary',
    filters,
    `Bonvi_davomat_xulosa_${rangeTag(filters.from, filters.to)}.xlsx`,
    signal,
  )
}

/**
 * Qayta hisoblash — `202 Accepted`, vazifa navbatga tushadi. Oraliq eng
 * ko'pi 92 kun (`davomat.davr_uzun`); ekran chegarani oldindan ham aytadi
 * (`MAX_REPORT_DAYS`).
 */
export async function startRecompute(body: RecomputeRequest): Promise<RecomputeResult> {
  return asSent(await recomputeAttendance(body))
}

/* ═════════════════════════════════════ qurilmaga yozish (SPEC-agent §D5) */

/**
 * Xodimning bitta qurilmadagi holati.
 *
 * `state` — SERVERDA hisoblanadi: bazadagi «agent oxirgi marta nima dedi»
 * emas, «hozir qanday». Rasm almashgan yoki «Qayta yozish» bosilgan xodim
 * shu sababli darhol «Pending» bo'lib ko'rinadi.
 */
export type DeviceEnrollmentRow = AsSent<DeviceEnrollmentRowDto>

/** Qurilma bo'yicha sanoqlar. */
export type DeviceEnrollmentSummary = AsSent<DeviceEnrollmentSummaryDto>

/** Qurilma sahifasining yozish bo'limi: sanoqlar + muammoli qatorlar. */
export type DeviceEnrollmentPage = AsSent<DeviceEnrollmentPageDto>

/** «Qayta yozish» natijasi. */
export type EnrollmentResync = AsSent<EnrollmentResyncDto>

export async function fetchDeviceEnrollments(
  deviceId: string,
  signal?: AbortSignal,
): Promise<DeviceEnrollmentPage> {
  return asSent(await getDeviceEnrollments(deviceId, signal))
}

/**
 * «Hammasini sinxronlash».
 *
 * ⚠️ Bu qurilmadagi odamlarni O'CHIRIB qayta yozish EMAS: agent baribir
 * har tekshiruvda ro'yxatni solishtiradi va yetishmaganini o'zi yozadi.
 * Tugma faqat XATO qatorlarni qayta urinishga qo'yadi (SPEC-agent §E).
 */
export async function postDeviceResync(deviceId: string): Promise<EnrollmentResync> {
  return asSent(await resyncDeviceEnrollments(deviceId))
}

/**
 * Ushlab qolingan ommaviy o'chirishni tasdiqlash — 10 daqiqaga.
 *
 * Agent ro'yxat bo'sh bo'lsa yoki terminaldagi Bonvi odamlarining beshdan
 * biridan ko'pi o'chishi kerak bo'lsa, o'chirmaydi (SPEC-agent §L). Bu
 * tugma to'siqni bir martaga ochadi; doimiy o'chirib qo'yish yo'q.
 */
export async function postApproveRemovals(deviceId: string): Promise<void> {
  await approveDeviceRemovals(deviceId)
}
