import { shiftSpanMinutes } from './attendance-presentation'
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
 * oyna smenadan tashqarida. Tartib boshqacha bo'lsa, bitta kun uchun
 * klient bir xatoni, server boshqasini aytardi.
 */

/** `WorkSchedule.MaxGraceMinutes` (SPEC §2.4). */
export const MAX_GRACE_MINUTES = 120

/** `WorkScheduleDay.MaxBreakMinutes` (SPEC §2.4). */
export const MAX_BREAK_MINUTES = 480

/** `work_schedules.name` — varchar(200). */
export const SCHEDULE_NAME_MAX = 200

/** `work_schedules.note` — varchar(500). */
export const SCHEDULE_NOTE_MAX = 500

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
  if (!hasStart && !hasEnd) return null
  if (hasStart !== hasEnd) return 'davomat.jadval_tanaffus_toliq_emas'

  const placement = breakWindowPlacement(day)
  return placement === null || !placement.inside ? 'davomat.jadval_tanaffus_oynasi' : null
}

/** Imtiyoz daqiqalari: butun son, `0 … 120` (`davomat.jadval_yon_berish`). */
export function isValidGrace(value: number): boolean {
  return Number.isInteger(value) && value >= 0 && value <= MAX_GRACE_MINUTES
}

/**
 * Bir kunning REJA daqiqalari: smena − tanaffus, manfiy emas.
 *
 * `weekSummary` bilan AYNAN bir formula (u shu kunlarning yig'indisi):
 * ekrandagi kunlik raqamlar qo'shilganda haftalik xulosaga teng
 * chiqishi shart, aks holda operator ikkalasidan biriga ishonmay qo'yadi.
 */
export function plannedDayMinutes(day: WorkScheduleDayInput): number {
  if (!day.isWorking) return 0
  return Math.max(0, shiftSpanMinutes(day) - day.breakMinutes)
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
