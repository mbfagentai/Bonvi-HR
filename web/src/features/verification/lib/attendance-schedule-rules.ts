import { formatDuration, shiftSpanMinutes } from './attendance-presentation'
import type {
  ScheduleAssignment,
  ScheduleResolution,
  WorkScheduleDayInput,
} from '../api/attendance-api'

/**
 * Ish jadvali muharririning OLDINDAN tekshiruvi — domen qoidalarining
 * klientdagi KO'ZGUSI.
 *
 * ⭐ NEGA endi klientda ham bor (F1 dastlab «takrorlanmaydi» degan edi):
 * egasining talabi — operator muammoni «Saqlash» ni bosib, serverdan rad
 * javobini olgandan KEYIN emas, yozayotgan paytda ko'rsin. Tungi smenadagi
 * tanaffus oynasi aynan shunday xato: 00:30 ciferblat bo'yicha 20:00 dan
 * «oldin», ya'ni odam uni noto'g'ri deb o'ylab, to'g'ri qiymatni o'chirib
 * yuborishi mumkin edi.
 *
 * ⚠️ HAQIQAT MANBASI — SERVER (`WorkScheduleDay.Validate`,
 * `WorkSchedule.Create`). Bu fayl faqat ogohlantiradi; server boshqacha
 * javob bersa, forma SERVERNING kodini ko'rsatadi, bu yerdagini emas.
 * Kodlar ham serverniki (SPEC §6.9) — tarjimasi bitta, ikkinchi matn
 * to'qilmaydi.
 *
 * Tekshiruv TARTIBI domen bilan bir xil (birinchi buzilgan qoida
 * qaytadi): vaqt bo'sh → vaqt teng → tanaffus uzunligi → yarim oyna →
 * oyna smenadan tashqarida → erkin kunning ish vaqti (SPEC-erkin-jadval
 * §2.5). Tartib boshqacha bo'lsa, bitta kun uchun klient bir xatoni,
 * server boshqasini aytardi.
 */

/** `WorkSchedule.MaxGraceMinutes` (SPEC §2.4). */
export const MAX_GRACE_MINUTES = 120

/** `WorkScheduleDay.MaxBreakMinutes` (SPEC §2.4). */
export const MAX_BREAK_MINUTES = 480

/** `work_schedules.name` — varchar(200). */
export const SCHEDULE_NAME_MAX = 200

/** `work_schedules.note` — varchar(500). */
export const SCHEDULE_NOTE_MAX = 500

/** `WorkScheduleDay.MinRequiredMinutes` — erkin kunning eng kam «Ish vaqti» (SPEC-erkin-jadval §2.5). */
export const MIN_REQUIRED_MINUTES = 1

/** Erkin kunning «Ish vaqti» chegaradan chiqdi — server kodi bilan bir xil. */
export const FLEXIBLE_REQUIRED_CODE = 'davomat.jadval_erkin_vaqti'

const DAY_MINUTES = 24 * 60

/** «09:30» → 570; yaroqsiz yoki bo'sh — `null`. */
export function clockMinutes(value: string | null): number | null {
  if (value === null) return null

  const match = /^(\d{1,2}):(\d{2})/u.exec(value)
  if (match === null) return null

  const hours = Number(match[1])
  const minutes = Number(match[2])
  if (hours > 23 || minutes > 59) return null

  return hours * 60 + minutes
}

/**
 * Tanaffus OYNASINING uzunligi, daqiqa — yarim tundan o'tib (23:30–00:30
 * = 60); oyna yo'q yoki to'liq emas — 0.
 *
 * Jadval so'rovi kunida tanaffus uzunligi berilmasa server aynan shuni oladi
 * (`ScheduleChangeRequest.BreakMinutes`, Verifix ma'nosi: oyna — tanaffusning o'zi).
 */
export function breakWindowMinutes(breakStart: string | null, breakEnd: string | null): number {
  const from = clockMinutes(breakStart)
  const to = clockMinutes(breakEnd)

  return from === null || to === null ? 0 : (to - from + DAY_MINUTES) % DAY_MINUTES
}

/**
 * Tanaffus oynasining SMENA O'QIDAGI o'rni — smena boshidan daqiqalarda
 * (SPEC §2.4.0.1).
 *
 * ⭐ Ciferblat qiymatlari TO'G'RIDAN-TO'G'RI SOLISHTIRILMAYDI: 20:00→08:00
 * smenada 00:30 «oldin» emas, smena boshlanganidan 270 daqiqa KEYIN.
 * `(t − boshlanish + 1440) mod 1440` — .NET dagi `TimeOnly` ayirmasi
 * bilan aynan bir xil aylanish, ya'ni ikki tomon bir xil javob beradi.
 *
 * `null` — kun ishlamaydi, vaqtlar to'liq emas yoki oyna yo'q.
 */
export interface BreakWindowPlacement {
  /** Oyna boshi, smena boshidan. */
  start: number
  /** Oyna oxiri, smena boshidan. */
  end: number
  /** Smena uzunligi (`(tugash − boshlanish + 1440) mod 1440`, hech qachon 0 emas). */
  shiftLength: number
  /** `0 <= start < end <= shiftLength` — domen shartining o'zi. */
  inside: boolean
}

export function breakWindowPlacement(day: WorkScheduleDayInput): BreakWindowPlacement | null {
  if (!day.isWorking) return null

  const shiftStart = clockMinutes(day.startTime)
  const shiftEnd = clockMinutes(day.endTime)
  const windowStart = clockMinutes(day.breakStart)
  const windowEnd = clockMinutes(day.breakEnd)

  if (shiftStart === null || shiftEnd === null || shiftStart === shiftEnd) return null
  if (windowStart === null || windowEnd === null) return null

  const offset = (value: number): number => (value - shiftStart + DAY_MINUTES) % DAY_MINUTES
  const shiftLength = offset(shiftEnd)
  const start = offset(windowStart)
  const end = offset(windowEnd)

  return {
    start,
    end,
    shiftLength,
    // `end === 0` — oyna AYNAN smena boshida tugaydi, qat'iy `<` uni rad
    // etadi; smena oxirida tugagan oyna esa ruxsat (`<=`) — §2.4.0.1.
    inside: start < end && end <= shiftLength,
  }
}

/**
 * Bir kunning birinchi buzilgan qoidasi — server kodi bilan; `null` —
 * kun joyida.
 *
 * Dam olish kuni HECH QACHON xato bermaydi: server uning vaqtlari va
 * oynasini jimgina TOZALAYDI (SPEC §2.4.0), ya'ni operator tuzatadigan
 * narsa yo'q. Forma ham ularni yubormaydi (`toDayInput`).
 */
export function validateScheduleDay(day: WorkScheduleDayInput): string | null {
  if (!day.isWorking) return null

  const start = clockMinutes(day.startTime)
  const end = clockMinutes(day.endTime)
  if (start === null || end === null) return 'davomat.jadval_vaqt_bosh'
  if (start === end) return 'davomat.jadval_vaqt_teng'

  if (
    !Number.isInteger(day.breakMinutes) ||
    day.breakMinutes < 0 ||
    day.breakMinutes > MAX_BREAK_MINUTES
  ) {
    return 'davomat.jadval_tanaffus_uzunligi'
  }

  const hasStart = day.breakStart !== null
  const hasEnd = day.breakEnd !== null
  if (hasStart !== hasEnd) return 'davomat.jadval_tanaffus_toliq_emas'

  if (hasStart) {
    const placement = breakWindowPlacement(day)
    if (placement === null || !placement.inside) return 'davomat.jadval_tanaffus_oynasi'
  }

  return validateRequired(day)
}

/**
 * Erkin kunning «Ish vaqti» — `WorkScheduleDay.ValidateRequired` ko'zgusi:
 * butun son, `1 … smena − tanaffus`. Berilmagan qiymat (`null`) sukutga —
 * eng kattasiga — teng va u ham shu yerdan o'tadi: tanaffus smenaga teng
 * yoki undan uzun bo'lsa sukut 0 bo'ladi va erkin kun rad etiladi.
 *
 * Forma o'qib bo'lmaydigan matnni `NaN` qilib yuboradi (`requiredMinutesOf`)
 * — u ham shu kod bilan ushlanadi, maydon esa aniqroq matnni ko'rsatadi.
 */
function validateRequired(day: WorkScheduleDayInput): string | null {
  if (!day.isFlexible) return null

  const max = maxRequiredMinutes(day)
  const required = day.requiredMinutes ?? max

  return !Number.isInteger(required) || required < MIN_REQUIRED_MINUTES || required > max
    ? FLEXIBLE_REQUIRED_CODE
    : null
}

/** Imtiyoz daqiqalari: butun son, `0 … 120` (`davomat.jadval_yon_berish`). */
export function isValidGrace(value: number): boolean {
  return Number.isInteger(value) && value >= 0 && value <= MAX_GRACE_MINUTES
}

/**
 * Bir kunning REJA daqiqalari: smena − tanaffus, manfiy emas; erkin kunda
 * esa «Ish vaqti» (R, berilmagan bo'lsa — sukut, ya'ni eng kattasi).
 *
 * `weekSummary` bilan AYNAN bir formula (u shu kunlarning yig'indisi):
 * ekrandagi kunlik raqamlar qo'shilganda haftalik xulosaga teng
 * chiqishi shart, aks holda operator ikkalasidan biriga ishonmay qo'yadi.
 *
 * Formada hali o'qib bo'lmaydigan R (`NaN`) — sukut bilan: xatoni maydon
 * aytadi, sarlavhadagi raqam esa «NaN» bo'lib chiqmasligi kerak.
 */
export function plannedDayMinutes(day: WorkScheduleDayInput): number {
  if (!day.isWorking) return 0
  if (!day.isFlexible) return Math.max(0, shiftSpanMinutes(day) - day.breakMinutes)

  const required = day.requiredMinutes
  return required !== null && Number.isFinite(required) ? required : maxRequiredMinutes(day)
}

/* ═════════════════════════════════════════════ erkin kun (SPEC-erkin-jadval) */

/**
 * Erkin kunning eng katta «Ish vaqti» — smena − tanaffus, manfiy emas
 * (`WorkScheduleDay.MaxRequiredMinutes`, §2.5). Server R berilmaganda
 * aynan shuni yozadi: 08:30–18:00, 120 daqiqa tanaffus → 450 (7:30).
 *
 * Jadval so'rovi kunida `breakMinutes` — samarali tanaffus (berilgan
 * uzunlik, aks holda oyna uzunligi), ya'ni chaqiruvchi uni shunday beradi.
 */
export function maxRequiredMinutes(day: WorkScheduleDayInput): number {
  return Math.max(0, shiftSpanMinutes(day) - day.breakMinutes)
}

/**
 * «7:30» → 450. Faqat `soat:daqiqa` (soat 0…23, daqiqa ikki xonali);
 * boshqasi — `null` (`attendance.durationFormat`).
 *
 * NEGA «7.5» yoki «450» qabul qilinmaydi: HR «yetti yarim» ni ikki xil
 * yozib, ikki xil son olardi; Verifix formasi ham faqat shu shaklni biladi.
 */
export function parseDuration(value: string): number | null {
  const match = /^(\d{1,2}):([0-5]\d)$/u.exec(value.trim())
  if (match === null) return null

  const hours = Number(match[1])
  return hours > 23 ? null : hours * 60 + Number(match[2])
}

/**
 * «Ish vaqti» maydonining FORMA holati — uchala HR formasida bir xil
 * (SPEC-erkin-jadval §7.1).
 *
 * `requiredAuto` — maydon sukutni (eng kattasini) ko'rsatadi va vaqtlar yoki
 * tanaffus o'zgarsa u bilan birga o'zgaradi; serverga `null` ketadi va
 * server R ni o'zi yozadi. HR qiymat yozsa — `false`, aynan eng kattasini
 * yozsa — yana `true`.
 */
export interface FlexibleDraft {
  isFlexible: boolean
  /** «7:30» — HR yozgan matn; `requiredAuto` da e'tiborsiz. */
  required: string
  requiredAuto: boolean
}

/** Erkin bo'lmagan kun — yangi qatorlar va dam olish kuni shu bilan boshlanadi. */
export const NOT_FLEXIBLE: FlexibleDraft = { isFlexible: false, required: '', requiredAuto: true }

/**
 * Saqlangan kun → forma. `max` — shu kunning eng kattasi (`maxRequiredMinutes`):
 * saqlangan R unga teng bo'lsa maydon sukutda turadi va vaqtlar o'zgarsa
 * R ham ergashadi — aks holda R qotib qolib, keyingi saqlashda xato berardi.
 */
export function flexibleDraftOf(
  isFlexible: boolean,
  requiredMinutes: number | null,
  max: number,
): FlexibleDraft {
  if (!isFlexible) return NOT_FLEXIBLE

  return {
    isFlexible: true,
    required: requiredMinutes === null ? '' : formatDuration(requiredMinutes),
    requiredAuto: requiredMinutes === null || requiredMinutes === max,
  }
}

/** Maydonda ko'rinadigan matn: sukutda — eng kattasi, aks holda HR yozgani. */
export function shownRequired(draft: FlexibleDraft, max: number): string {
  if (!draft.requiredAuto) return draft.required
  // «07:30» yozib sukutga qaytgan HR o'z yozganini ko'radi — matn qo'l ostida o'zgarmasin
  return parseDuration(draft.required) === max ? draft.required : formatDuration(max)
}

/** HR maydonga yozdi. Aynan eng kattasi — yana sukut (`null` yuboriladi). */
export function typedRequired(draft: FlexibleDraft, text: string, max: number): FlexibleDraft {
  return { ...draft, required: text, requiredAuto: parseDuration(text) === max }
}

/**
 * Yuboriladigan R: `null` — server sukuti (erkin emas yoki maydon sukutda);
 * o'qib bo'lmaydigan matn — `NaN`, tekshiruv uni ushlaydi va forma
 * yubormaydi (`parseMinutes` bilan bir xil qoida).
 */
export function requiredMinutesOf(draft: FlexibleDraft): number | null {
  if (!draft.isFlexible || draft.requiredAuto) return null
  return parseDuration(draft.required) ?? Number.NaN
}

/** HR yozgan matn `soat:daqiqa` emas — maydon `attendance.durationFormat` ni ko'rsatadi. */
export function requiredUnparsable(draft: FlexibleDraft): boolean {
  return draft.isFlexible && !draft.requiredAuto && parseDuration(draft.required) === null
}

/* ═════════════════════════════════════════════ «Kim uchun amal qiladi» */

export type ScheduleOutcome = 'notEmployed' | 'noSchedule' | 'resolved'

/**
 * Yechim natijasi — FAQAT ikki `null` dan, SPEC §6.4 dagi tartibda.
 *
 * NEGA `workScheduleId` ga qaralmaydi: shartnoma natijani aynan
 * `employmentId` va `scope` dan o'qishni belgilaydi. Uchinchi maydonni
 * belgi qilib olish — server bir kuni jadval nomini bo'sh yuborganda
 * «jadval yo'q» deb yolg'on aytish demak.
 */
export function scheduleOutcome(resolution: ScheduleResolution): ScheduleOutcome {
  if (resolution.employmentId === null) return 'notEmployed'
  if (resolution.scope === null) return 'noSchedule'
  return 'resolved'
}

/**
 * Jadval OTA bo'limdan meros kelganmi — yutgan bo'lim xodimning o'z bo'limi
 * emas (SPEC §5.2, eng yaqin ajdod birinchi).
 */
export function inheritedFromParent(resolution: ScheduleResolution): boolean {
  return (
    resolution.scope === 'Department' &&
    resolution.matchedDepartmentId !== null &&
    resolution.matchedDepartmentId !== resolution.departmentId
  )
}

/**
 * Biriktirma shu sanada KUCHDAMI: `effectiveFrom <= sana <= effectiveTo`
 * (ikki tomondan yopiq, `null` oxir — muddatsiz).
 *
 * `DateOnly` satrlari «YYYY-MM-DD» — leksik tartib sana tartibi bilan bir
 * xil, ya'ni `Date` ga o'girish (va u bilan birga keladigan mintaqa
 * xatosi) kerak emas.
 */
export function assignmentInForce(assignment: ScheduleAssignment, date: string): boolean {
  if (assignment.effectiveFrom > date) return false
  return assignment.effectiveTo === null || assignment.effectiveTo >= date
}
