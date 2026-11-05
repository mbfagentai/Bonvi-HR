import type { ProcessStepState } from '../api/processes-api'

export type ProcessDue =
  | { kind: 'late'; days: number }
  | { kind: 'today' }
  | { kind: 'tomorrow' }
  | { kind: 'until'; date: string }
  | { kind: 'none' }

const DAY_MS = 86_400_000

function dayNumber(isoDate: string): number {
  const [year = 0, month = 1, day = 1] = isoDate.split('-').map(Number)
  return Date.UTC(year, month - 1, day) / DAY_MS
}

/**
 * The due chip's meaning (SPEC §11.2): «3 kun kechikdi», «Bugun», «Ertaga», «24.11 gacha». `daysLate` is the server's
 * count and wins, because it may skip non-working days. `today` is the same day the server used
 * (`ProcessMyWork.today`, else `todayIso()`), never the browser's own clock zone.
 */
export function describeDue(dueDate: string | null, daysLate: number, today: string): ProcessDue {
  if (daysLate > 0) return { kind: 'late', days: daysLate }
  if (dueDate === null) return { kind: 'none' }

  const left = dayNumber(dueDate) - dayNumber(today)
  if (left <= 0) return { kind: 'today' }
  if (left === 1) return { kind: 'tomorrow' }
  return { kind: 'until', date: dueDate }
}

/** «2026-11-24» → «24.11» («11-24» in English, matching `formatDate`). */
export function shortDueDate(isoDate: string, locale: string): string {
  const [, month = '', day = ''] = isoDate.split('-')
  return locale === 'en' ? `${month}-${day}` : `${day}.${month}`
}

export type ProcessTaskTone = 'late' | 'today' | 'blocked' | 'normal'

/** The task card's colour stripe (SPEC §11.2): red late, yellow today, grey blocked. A block outranks lateness. */
export function taskTone(
  state: ProcessStepState,
  dueDate: string | null,
  daysLate: number,
  today: string,
): ProcessTaskTone {
  if (state === 'Blocked') return 'blocked'

  const due = describeDue(dueDate, daysLate, today).kind
  if (due === 'late') return 'late'
  return due === 'today' ? 'today' : 'normal'
}
