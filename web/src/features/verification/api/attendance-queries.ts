import { useMutation, useQuery } from '@tanstack/react-query'

import { saveBlobAs } from '@/shared/lib/download'

import {
  confirmIdentityImport,
  createAssignment,
  createDevice,
  createIdentity,
  deleteAssignment,
  deleteDevice,
  deleteIdentity,
  deleteSchedule,
  exportExceptions,
  exportPasses,
  exportSummary,
  exportTimesheet,
  fetchAssignments,
  fetchDashboard,
  fetchDevice,
  fetchDevices,
  fetchExceptions,
  fetchFormerEmployeeAlerts,
  fetchIdentities,
  fetchPass,
  fetchPassPhoto,
  fetchPasses,
  fetchSchedule,
  fetchSchedules,
  fetchSummary,
  fetchTimesheet,
  fetchUnmatched,
  previewIdentityImport,
  resolveSchedule,
  rotateDeviceToken,
  saveSchedule,
  setDeviceActive,
  setScheduleActive,
  startRecompute,
  updateDevice,
  type AssignmentFilters,
  type AttendanceDeviceRequest,
  type AttendanceIdentityProvider,
  type DashboardFilters,
  type FormerEmployeeAlert,
  type CreateIdentityRequest,
  type ExceptionFilters,
  type ExceptionListFilters,
  type ImportConfirmRequest,
  type IdentityListFilters,
  type PageParams,
  type PassFilters,
  type PassListFilters,
  type RecomputeRequest,
  type ScheduleAssignmentRequest,
  type SummaryFilters,
  type SummaryListFilters,
  type TimesheetFilters,
  type TimesheetListFilters,
  type WorkScheduleRequest,
} from './attendance-api'
import { BOARD_REFRESH_MS, UNMATCHED_COUNT_REFRESH_MS } from '../lib/attendance-presentation'

/**
 * Kesh kalitlari.
 *
 * Barchasi `['attendance', …]` bilan boshlanadi — `recruitingKeys` va
 * `incidentKeys` bilan bir xil naqsh. Filtrlar kalitning BIR QISMI:
 * filtr o'zgarsa yangi so'rov ketadi, eskisi esa keshda qoladi va HR
 * filtrlar orasida yurganda ekran darhol to'ladi.
 *
 * ⚠️ Mutatsiyalar QO'LDA `invalidateQueries` YOZMAYDI: `providers.tsx`
 * har qanday muvaffaqiyatli yozuv amalidan keyin butun keshni (rasm
 * kalitlaridan tashqari) o'zi bekor qiladi. Sabab o'sha faylda batafsil:
 * «nima eskirishini sanab chiq» qoidasi doimiy unutilardi.
 *
 * ⭐ AYNAN SHU SABABDAN o'tish suratining kalit ildizi `-photo` bilan
 * TUGAYDI. `providers.tsx` umumiy bekor qilishdan faqat shu shakldagi
 * kalitlarni chetlab o'tadi (`/-photo$/`). Busiz jurnaldagi har bir
 * yozuv amali ekrandagi 25 ta suratni qaytadan yuklatardi — foydasi
 * nol, narxi katta.
 */
export const attendanceKeys = {
  root: ['attendance'] as const,

  devices: (includeInactive: boolean) => ['attendance', 'devices', includeInactive] as const,
  device: (id: string) => ['attendance', 'device', id] as const,

  identities: (filters: IdentityListFilters) =>
    ['attendance', 'identities', filters] as const,
  /**
   * Moslanmagan guruhlar. Menyudagi to'q sariq sanoq ham SHU so'rovdan
   * oziqlanadi (`total`) — ikkinchi endpoint yo'q va kerak ham emas.
   */
  unmatched: (page: PageParams) => ['attendance', 'unmatched', page] as const,

  schedules: ['attendance', 'schedules'] as const,
  schedule: (id: string) => ['attendance', 'schedule', id] as const,
  assignments: (filters: AssignmentFilters) =>
    ['attendance', 'assignments', filters] as const,
  resolution: (employeeId: string, date: string) =>
    ['attendance', 'resolve', employeeId, date] as const,

  passes: (filters: PassListFilters) => ['attendance', 'passes', filters] as const,
  /** Bitta o'tish — `?passId=` havolasi uchun (ro'yxat keshidan ALOHIDA). */
  pass: (id: string) => ['attendance', 'pass', id] as const,
  dashboard: (filters: DashboardFilters) => ['attendance', 'dashboard', filters] as const,
  /**
   * Ishdan ketganlar ogohlantirishi — menyu nishoni va statistika sahifasi
   * BITTA kalitni bo'lishadi: ikkalasi bir vaqtda ekranda turganda
   * so'rov bir marta ketadi va nishondagi son ogohlantirishdagi qatorlar
   * soni bilan hech qachon farq qilmaydi.
   */
  formerEmployeeAlerts: ['attendance', 'former-employee-alerts'] as const,

  timesheet: (filters: TimesheetListFilters) =>
    ['attendance', 'timesheet', filters] as const,
  exceptions: (filters: ExceptionListFilters) =>
    ['attendance', 'exceptions', filters] as const,
  summary: (filters: SummaryListFilters) => ['attendance', 'summary', filters] as const,

  /** Surat keshi — ildiz ATAYLAB `-photo` bilan tugaydi (yuqoridagi izoh). */
  photos: (passId: string) => ['attendance-pass-photo', passId] as const,
  photo: (passId: string, photoKey: string | null) =>
    ['attendance-pass-photo', passId, photoKey] as const,
}

/* ═════════════════════════════════════════════════════ qurilmalar (§6.2) */

/**
 * @param enabled `false` — so'rov UMUMAN yuborilmaydi.
 *
 * NEGA har bir hook'da bor: qurilmalar va mosliklar `attendance.manage`
 * ostida (SPEC §7.3), ya'ni faqat `attendance.view` ga ega foydalanuvchi
 * bu so'rovlarni yuborsa, har ochilishda 403 olardi. Ruxsat tekshiruvi
 * CHAQIRUVCHIDA — `useEmployeeIncidents` dagi bilan bir xil kelishuv.
 */
export function useAttendanceDevices(includeInactive: boolean, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.devices(includeInactive),
    queryFn: ({ signal }) => fetchDevices(includeInactive, signal),
    enabled,
  })
}

export function useAttendanceDevice(id: string, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.device(id),
    queryFn: ({ signal }) => fetchDevice(id, signal),
    enabled: enabled && id !== '',
  })
}

/**
 * Qurilma yaratish — javobda TOKEN qaytadi va u boshqa ko'rsatilmaydi.
 *
 * ⚠️ Natijani keshga YOZMANG. Chaqiruvchi uni faqat oynaning o'z
 * holatida ushlab turadi va oyna yopilishi bilan token yo'qoladi —
 * aynan shunday bo'lishi kerak (SPEC §3.2).
 */
export function useCreateDevice() {
  return useMutation({
    mutationFn: (body: AttendanceDeviceRequest) => createDevice(body),
  })
}

export function useUpdateDevice() {
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: AttendanceDeviceRequest }) =>
      updateDevice(id, body),
  })
}

/** Eski token SHU ZAHOTI o'ladi — tasdiqlash oynasisiz chaqirilmaydi. */
export function useRotateDeviceToken() {
  return useMutation({
    mutationFn: (id: string) => rotateDeviceToken(id),
  })
}

export function useSetDeviceActive() {
  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      setDeviceActive(id, active),
  })
}

export function useDeleteDevice() {
  return useMutation({
    mutationFn: (id: string) => deleteDevice(id),
  })
}

/* ═══════════════════════════════════════════════════════ mosliklar (§6.3) */

export function useAttendanceIdentities(filters: IdentityListFilters, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.identities(filters),
    queryFn: ({ signal }) => fetchIdentities(filters, signal),
    enabled,
    // Qidiruv o'zgarganda jadval bo'shab «hech narsa topilmadi» bo'lib
    // ko'rinmasligi kerak — eski natija yangisi kelguncha turadi.
    placeholderData: (previous) => previous,
  })
}

export function useUnmatchedPasses(page: PageParams, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.unmatched(page),
    queryFn: ({ signal }) => fetchUnmatched(page, signal),
    enabled,
    placeholderData: (previous) => previous,
  })
}

/** Modul darajasida — kalit har renderda yangi obyekt bo'lib qolmasin. */
const UNMATCHED_COUNT_PAGE: PageParams = { page: 1, pageSize: 1 }

/**
 * Menyudagi to'q sariq sanoq uchun — moslanmagan guruhlar SONI.
 *
 * ⭐ Alohida endpoint YO'Q va kerak emas: ro'yxatning `total` i aynan
 * shu raqam (SPEC §8.1). Eng kichik sahifa so'raladi — bizni faqat
 * sarhisob qiziqtiradi, qatorlar emas.
 *
 * Kalit ro'yxatniki bilan BIR XIL bo'lishi shart emas va ataylab
 * boshqacha (`pageSize: 1`): moslashtirish sahifasi ochilganda u
 * o'zining to'liq ro'yxatini so'raydi, sanoq esa menyuda qolaveradi.
 *
 * NEGA davriy so'rov: nishon menyuda HAR ekranda turadi, yangi raqam esa
 * turniketdan istalgan payt keladi. Busiz sanoq faqat oyna fokusga
 * qaytganda yoki kesh eskirib, kimdir sahifa almashtirganda yangilanardi
 * — HR bir soat bitta ekranda o'tirsa, nishon o'sha soat davomida eski
 * raqamni ko'rsatardi. 60 soniya (ogohlantirishdagi 30 dan sekinroq)
 * yetarli: bu xavfsizlik hodisasi emas, bajariladigan ishlar navbati.
 *
 * `placeholderData` — yangilanish orasida nishon «0» ga tushib, keyin
 * qayta paydo bo'lib miltillamasin.
 */
export function useUnmatchedCount(enabled = true): number {
  const query = useQuery({
    queryKey: attendanceKeys.unmatched(UNMATCHED_COUNT_PAGE),
    queryFn: ({ signal }) => fetchUnmatched(UNMATCHED_COUNT_PAGE, signal),
    enabled,
    refetchInterval: UNMATCHED_COUNT_REFRESH_MS,
    placeholderData: (previous) => previous,
  })

  return query.data?.total ?? 0
}

export function useCreateIdentity() {
  return useMutation({
    mutationFn: (body: CreateIdentityRequest) => createIdentity(body),
  })
}

export function useDeleteIdentity() {
  return useMutation({
    mutationFn: (id: string) => deleteIdentity(id),
  })
}

/**
 * Faylni oldindan ko'rish.
 *
 * NEGA `useQuery` emas, `useMutation`: fayl FOYDALANUVCHI tanlaganda
 * bir marta yuboriladi — so'rovni sahifa ochilishida yoki oyna fokusga
 * qaytganda takrorlash ma'nosiz (`useExportEmployees` dagi bilan bir
 * xil mulohaza).
 *
 * `readOnly` bayrog'i ham shu sababdan: server HECH NARSA YOZMAYDI
 * (SPEC §4.4), ya'ni butun keshni bekor qilish faqat oyna ortidagi
 * ro'yxatlarni bekorga qayta yuklatardi.
 */
export function usePreviewIdentityImport() {
  return useMutation({
    mutationFn: ({ file, provider }: { file: File; provider: AttendanceIdentityProvider }) =>
      previewIdentityImport(file, provider),
    meta: { readOnly: true },
  })
}

/** Tasdiqlash — mosliklar yaratiladi va har biri backfill'ni qo'zg'atadi (§4.3). */
export function useConfirmIdentityImport() {
  return useMutation({
    mutationFn: (body: ImportConfirmRequest) => confirmIdentityImport(body),
  })
}

/* ═══════════════════════════════════════════════════════ jadvallar (§6.4) */

export function useWorkSchedules(enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.schedules,
    queryFn: ({ signal }) => fetchSchedules(signal),
    enabled,
  })
}

export function useWorkSchedule(id: string, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.schedule(id),
    queryFn: ({ signal }) => fetchSchedule(id, signal),
    enabled: enabled && id !== '',
  })
}

/** Yaratish va tahrirlash bitta mutatsiya: `id === null` — yangi jadval. */
export function useSaveSchedule() {
  return useMutation({
    mutationFn: ({ id, body }: { id: string | null; body: WorkScheduleRequest }) =>
      saveSchedule(id, body),
  })
}

export function useDeleteSchedule() {
  return useMutation({
    mutationFn: (id: string) => deleteSchedule(id),
  })
}

/**
 * Jadvalni yoqish/o'chirish.
 *
 * ⚠️ Biriktirmasi bor jadvalni server faolsizlantirmaydi
 * (`davomat.jadval_biriktirilgan`, 409) — chaqiruvchi rad javobini
 * o'chirishdagi bilan bir xil tushuntirish bilan ko'rsatadi.
 */
export function useSetScheduleActive() {
  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setScheduleActive(id, active),
  })
}

export function useScheduleAssignments(filters: AssignmentFilters, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.assignments(filters),
    queryFn: ({ signal }) => fetchAssignments(filters, signal),
    enabled,
  })
}

export function useCreateAssignment() {
  return useMutation({
    mutationFn: (body: ScheduleAssignmentRequest) => createAssignment(body),
  })
}

export function useDeleteAssignment() {
  return useMutation({
    mutationFn: (id: string) => deleteAssignment(id),
  })
}

/**
 * «Nega bu odamda shu jadval?» — yutgan jadval va yutgan daraja.
 *
 * `enabled` xodim TANLANGANDA yoqiladi: oyna ochilishining o'zida
 * so'rov yuborish ma'nosiz, chunki hali kim haqida so'ralayotgani
 * noma'lum.
 */
export function useScheduleResolution(employeeId: string, date: string, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.resolution(employeeId, date),
    queryFn: ({ signal }) => resolveSchedule(employeeId, date, signal),
    enabled: enabled && employeeId !== '' && date !== '',
  })
}

/* ═══════════════════════════════════════════════════════ o'tishlar (§6.5) */

export function useAttendancePasses(filters: PassListFilters, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.passes(filters),
    queryFn: ({ signal }) => fetchPasses(filters, signal),
    enabled,
    // Sahifadan sahifaga o'tganda jadval bo'shamaydi — eski ma'lumot
    // ko'rinib turadi va faqat ustidagi holat «yangilanmoqda» bo'ladi.
    placeholderData: (previous) => previous,
  })
}

/**
 * Bitta o'tish — `?passId=` havolasi ochganda (SPEC §8.4).
 *
 * `retry: false`: eng ko'p kutiladigan xato — 404 `davomat.otish_topilmadi`
 * (saqlash muddati tugab o'chirilgan o'tish), uni uch marta qayta so'rash
 * oynani bir necha soniya «yuklanmoqda» holatida ushlab turardi, natija
 * esa o'zgarmasdi.
 */
export function useAttendancePass(id: string, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.pass(id),
    queryFn: ({ signal }) => fetchPass(id, signal),
    enabled: enabled && id !== '',
    retry: false,
  })
}

/**
 * O'tish surati.
 *
 * Kalitda `photoKey` turadi — kesh o'z-o'zidan to'g'ri saqlanadi
 * (`useEmployeePhoto` bilan bir xil qoida): surat tozalansa kalit ham
 * boshqa bo'ladi va eski blob'ga hech kim murojaat qilmaydi.
 */
export function useAttendancePassPhoto(passId: string, photoKey: string | null) {
  return useQuery({
    queryKey: attendanceKeys.photo(passId, photoKey),
    queryFn: ({ signal }) => fetchPassPhoto(passId, signal),
    /*
      Kaliti yo'q — surati ham yo'q (qurilma yubormagan yoki tozalash
      vazifasi o'chirgan), so'rov umuman yuborilmaydi. Busiz 25 qatorli
      jurnal har ochilganda o'nlab keraksiz 404 yuborardi.
    */
    enabled: passId !== '' && photoKey !== null,
    /** Bir kalit — bir surat. U o'zgarmaydi, demak eskirmaydi ham. */
    staleTime: Infinity,
    /*
      Qayta urinish yo'q: surat chiqmasa o'rnida belgisi qoladi va HR
      ishida hech narsa to'xtamaydi (`useEmployeePhoto` dagi sabab).
    */
    retry: false,
  })
}

/**
 * Davomat statistikasi — diagramma va jadval bitta so'rovdan.
 *
 * @param live `true` — tanlangan kun BUGUN: 30 soniyada bir yangilanadi
 * (SPEC §1.2 #9) va `staleTime: 0`. O'tgan kun o'zgarmaydi, uni qayta
 * so'rash faqat serverni bekorga yuklardi — shu sababli u so'ralmaydi.
 *
 * ⭐ `placeholderData` SHART: har yarim daqiqada diagramma bo'shab, keyin
 * qayta chizilsa, sahifani hech kim ochiq qoldirmaydi (SPEC §8.5 — «no
 * spinner churn»). Kun, filtr yoki sahifa almashganda ham eski surat
 * yangisi kelguncha turadi — sakrash o'rniga yangilanish ko'rinadi.
 *
 * `staleTime: 0` faqat jonli rejimda: umumiy standart 60 soniya
 * (`providers.tsx`), ya'ni ekranga qaytilganda bugungi raqamlar bir
 * daqiqagacha eski bo'lib turardi.
 */
export function useAttendanceDashboard(filters: DashboardFilters, live: boolean, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.dashboard(filters),
    queryFn: ({ signal }) => fetchDashboard(filters, signal),
    enabled,
    refetchInterval: live ? BOARD_REFRESH_MS : false,
    ...(live ? { staleTime: 0 } : {}),
    placeholderData: (previous) => previous,
  })
}

const NO_ALERTS: FormerEmployeeAlert[] = []

/**
 * Ishdan ketganlarning so'nggi 24 soatdagi o'tishlari — TO'LIQ so'rov
 * holati bilan (SPEC §6.5 / §8.7 — AMENDED 2026-09-17).
 *
 * ⭐ `GET /former-employee-alerts` — taxta EMAS. Menyu nishoni har
 * ekranda turadi va har 30 soniyada so'raydi; taxta so'rovi esa har
 * safar ichkaridagilar, kutilganlar va kechikkanlarni qayta hisoblatardi.
 * Server ikkala javobni BITTA metoddan beradi, ya'ni ular bir-biriga zid
 * chiqa olmaydi.
 *
 Statistika sahifasi ham ogohlantirishni SHU hook'dan chizadi — ikki
 * sabab: (1) nishon va qizil blok bitta kesh yozuvidan o'qiydi, ya'ni
 * «nishonda 2, blokda 1» degan holat bo'lmaydi; (2) statistikaning og'ir
 * hisobi yiqilsa ham xavfsizlik ogohlantirishi ekrandan yo'qolmaydi.
 *
 * `placeholderData` yo'q, lekin `staleTime: 0` bor: blok har 30 soniyada
 * yangilanadi va o'rtada bo'shab qolmaydi (kesh yozuvi o'z joyida turadi).
 *
 * @param enabled `attendance.view` bo'lmagan foydalanuvchida `false` —
 * aks holda uning har bir ekrani har 30 soniyada 403 olardi (§8.7).
 */
export function useFormerEmployeeAlertsQuery(enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.formerEmployeeAlerts,
    queryFn: ({ signal }) => fetchFormerEmployeeAlerts(signal),
    enabled,
    refetchInterval: BOARD_REFRESH_MS,
    staleTime: 0,
  })
}

/**
 * Faqat qatorlar — menyu nishoni va qurilmalar sahifasidagi qizil qator
 * uchun, ularga so'rov holati kerak emas. Sanoq — `.length` (odamlar).
 *
 * Imzo ATAYLAB o'zgartirilmadi (ilgari taxta orqali ishlardi): chaqiruvchilar
 * tegilmasdan qoladi, faqat manba yengil endpoint'ga ko'chdi.
 */
export function useFormerEmployeeAlerts(enabled = true): FormerEmployeeAlert[] {
  return useFormerEmployeeAlertsQuery(enabled).data?.items ?? NO_ALERTS
}

/* ═════════════════════════════════════════════════════ hisobotlar (§6.6) */

export function useTimesheet(filters: TimesheetListFilters, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.timesheet(filters),
    queryFn: ({ signal }) => fetchTimesheet(filters, signal),
    enabled,
    /*
      Oy almashtirilganda grid bo'shab ketmaydi. Bu yerda bu ayniqsa
      muhim: 31 ustunli jadval yo'qolib qayta chizilsa, sahifa
      sakraydi va ustun kengliklari «raqsga tushadi» (SPEC §8.6).
    */
    placeholderData: (previous) => previous,
  })
}

export function useAttendanceExceptions(filters: ExceptionListFilters, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.exceptions(filters),
    queryFn: ({ signal }) => fetchExceptions(filters, signal),
    enabled,
    placeholderData: (previous) => previous,
  })
}

export function useAttendanceSummary(filters: SummaryListFilters, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.summary(filters),
    queryFn: ({ signal }) => fetchSummary(filters, signal),
    enabled,
    placeholderData: (previous) => previous,
  })
}

/* ═══════════════════════════════════════ eksport va qayta hisoblash (§6.7) */

/*
 * To'rtala eksport ham bir xil shaklda: mutatsiya + `readOnly` + faylni
 * diskka saqlash.
 *
 * NEGA `useQuery` emas: so'rov FOYDALANUVCHI BOSGANDA va faqat bir
 * marta ketishi kerak. `useQuery` sahifa ochilishining o'zida butun
 * oylik tabelni so'rab yuborardi, oyna fokusga qaytganda esa yana
 * takrorlardi — serverda esa eksportga tezlik cheklovi bor
 * (`RateLimitPolicies.Export`), ya'ni bunday so'rovlar HR ning haqiqiy
 * urinishini 429 bilan to'sib qo'yardi (`useExportEmployees` dagi izoh).
 *
 * `readOnly` bayrog'i `providers.tsx` dagi umumiy kesh bekor qilishdan
 * chetlab o'tkazadi: natija ekranga emas, DISKKA tushadi.
 */

export function useExportTimesheet() {
  return useMutation({
    mutationFn: (filters: TimesheetFilters) => exportTimesheet(filters),
    onSuccess: ({ blob, fileName }) => saveBlobAs(blob, fileName),
    meta: { readOnly: true },
  })
}

export function useExportPasses() {
  return useMutation({
    mutationFn: (filters: PassFilters) => exportPasses(filters),
    onSuccess: ({ blob, fileName }) => saveBlobAs(blob, fileName),
    meta: { readOnly: true },
  })
}

export function useExportExceptions() {
  return useMutation({
    mutationFn: (filters: ExceptionFilters) => exportExceptions(filters),
    onSuccess: ({ blob, fileName }) => saveBlobAs(blob, fileName),
    meta: { readOnly: true },
  })
}

export function useExportSummary() {
  return useMutation({
    mutationFn: (filters: SummaryFilters) => exportSummary(filters),
    onSuccess: ({ blob, fileName }) => saveBlobAs(blob, fileName),
    meta: { readOnly: true },
  })
}

/**
 * Qayta hisoblash.
 *
 * `readOnly` QO'YILMAYDI — vazifa `attendance_days` ni qayta yozadi,
 * ya'ni tabel, hisobotlar va statistika haqiqatan eskiradi. Umumiy bekor
 * qilish aynan shu yerda kerak.
 *
 * ⚠️ Javob «bajarildi» degani EMAS (`202 Accepted`, SPEC §6.7):
 * ekrandagi xabar «hisoblash boshlandi» bo'lishi kerak, aks holda HR
 * darhol tabelga qarab, o'zgarmagan raqamni ko'radi va buni nosozlik
 * deb o'ylaydi.
 */
export function useRecompute() {
  return useMutation({
    mutationFn: (body: RecomputeRequest) => startRecompute(body),
  })
}
