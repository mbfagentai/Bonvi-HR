import type { AbsenceRequestStatus, AttendanceDayStatus, MarkSource, TeamBoardStatus } from '@/api/types'

/**
 * Holatning ko'rinishi — UX §D.4 jadvalining aynan o'zi.
 *
 * ⚠️ Ma'nolar HR ilovasidan QIYMAT bo'lib ko'chirilgan va o'z-o'zidan
 * siljimasligi kerak: `Sababli` ikkala ilovada ham ko'k, chunki
 * tasdiqlangan yo'qlik — muammo emas, hujjatlashtirilgan ma'lumot;
 * `Kelmadi` ikkalasida ham qizil. HR ma'noni o'zgartirsa, ikkala
 * jadval ham QO'LDA, ataylab, birga o'zgaradi.
 *
 * ⚠️ `Record<...>` ataylab to'liq: backend enum'ga yangi qiymat
 * qo'shilsa, bu fayl kompilyatsiyadan o'tmaydi — xato ekranga emas,
 * build'ga chiqadi (SPEC §D4).
 */
export interface StatusLook {
  /** Tarjima kaliti — rang emas, SO'Z ma'noni tashiydi. */
  labelKey: string
  /** Nuqtaning rangi; `null` — oddiy holat, rang sarflanmaydi. */
  dotClass: string | null
  /** Katak yoki chip foni. */
  tintClass: string
}

export const DAY_STATUS_LOOK: Record<AttendanceDayStatus, StatusLook> = {
  // «Ishladi» da rang YO'Q — bu odatiy hol. Verifix taqvimining
  // sariq devoriga aynan shu javob (UX §D.1, 1-nuqson).
  Present: { labelKey: 'status.present', dotClass: null, tintClass: 'bg-bg-elevated' },
  Absent: { labelKey: 'status.absent', dotClass: 'bg-hue-absent', tintClass: 'bg-tint-absent' },
  DayOff: { labelKey: 'status.dayOff', dotClass: 'bg-hue-dayoff', tintClass: 'bg-tint-dayoff' },
  DayOffWorked: {
    labelKey: 'status.dayOffWorked',
    dotClass: 'bg-hue-dayoff-worked',
    tintClass: 'bg-tint-dayoff-worked',
  },
  MissingExit: { labelKey: 'status.missingExit', dotClass: 'bg-hue-late', tintClass: 'bg-tint-late' },
  MissingEntry: {
    labelKey: 'status.missingEntry',
    dotClass: 'bg-hue-late',
    tintClass: 'bg-tint-late',
  },
  NoSchedule: { labelKey: 'status.noSchedule', dotClass: null, tintClass: 'bg-bg-sunken' },
  NotEmployed: { labelKey: 'status.notEmployed', dotClass: null, tintClass: 'bg-bg-sunken' },
  Excused: { labelKey: 'status.excused', dotClass: 'bg-hue-excused', tintClass: 'bg-tint-excused' },
  Planned: { labelKey: 'status.planned', dotClass: null, tintClass: 'bg-bg-elevated' },
}

export const REQUEST_STATUS_LOOK: Record<AbsenceRequestStatus, StatusLook> = {
  Pending: { labelKey: 'status.pending', dotClass: 'bg-hue-late', tintClass: 'bg-tint-late' },
  Approved: { labelKey: 'status.approved', dotClass: 'bg-hue-success', tintClass: 'bg-tint-success' },
  Rejected: { labelKey: 'status.rejected', dotClass: 'bg-hue-absent', tintClass: 'bg-tint-absent' },
  Cancelled: { labelKey: 'status.cancelled', dotClass: 'bg-hue-dayoff', tintClass: 'bg-tint-dayoff' },
}

/**
 * Jamoa panelidagi holat (SPEC-jamoam §F) — ranglar TAQVIMDAN olinadi, yangi
 * rang to'qilmaydi: boshliq ko'rgan «Kelmadi» xodimning o'z taqvimidagi
 * «Kelmadi» bilan bir xil qizil. Kelganlar (vaqtida yoki jadvalsiz) —
 * `Ishladi` kabi RANGSIZ: rang faqat e'tibor talab qiladigan odamga
 * sarflanadi; kechikish — taqvimdagi kechikish chizig'ining sariq rangi.
 */
export const TEAM_STATUS_LOOK: Record<TeamBoardStatus, StatusLook> = {
  OnTime: { ...DAY_STATUS_LOOK.Present, labelKey: 'team.status.onTime' },
  Late: { labelKey: 'team.status.late', dotClass: 'bg-hue-late', tintClass: 'bg-tint-late' },
  Arrived: { ...DAY_STATUS_LOOK.Present, labelKey: 'team.status.arrived' },
  Absent: { ...DAY_STATUS_LOOK.Absent, labelKey: 'team.status.absent' },
  Excused: { ...DAY_STATUS_LOOK.Excused, labelKey: 'team.status.excused' },
  // Smena hali boshlanmagan — taqvimdagi «Kun kelmagan» kabi sokin
  NotStarted: { ...DAY_STATUS_LOOK.Planned, labelKey: 'team.status.notStarted' },
  DayOff: { ...DAY_STATUS_LOOK.DayOff, labelKey: 'team.status.dayOff' },
  NoSchedule: { ...DAY_STATUS_LOOK.NoSchedule, labelKey: 'team.status.noSchedule' },
}

/** O'tish manbasi: `Turniket` yoki `Mobil ilova` (UX §F.7). */
export const MARK_SOURCE_KEY: Record<MarkSource, string> = {
  Terminal: 'mark.sourceTerminal',
  Mobile: 'mark.sourceMobile',
}
